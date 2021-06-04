// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData.Common
{
    public class Log
    {
        public static int LogErrors = 0;
        private static ConfigurationOptions _config = new ConfigurationOptions();
        private static bool _isRunning;
        private static JsonSerializerSettings _jsonSerializerSettings;
        private static SynchronizedList<LogMessage> _lastMessageList = new SynchronizedList<LogMessage>();
        private static int _logDebug = LoggingLevel.Info;
        private static string _logFile;
        private static bool _logFileEnabled;
        private static SynchronizedList<LogMessage> _messageList = new SynchronizedList<LogMessage>();
        private static StreamWriter _streamWriter;
        private static Task _taskWriter;
        private static int _threadSleepMs = Constants.ThreadSleepMs100;

        public delegate void LogMessageHandler(object sender, LogMessage args);

        public static event LogMessageHandler MessageLogged;

        private static event EventHandler<Newtonsoft.Json.Serialization.ErrorEventArgs> JsonErrorHandler;

        public static ConfigurationOptions Config
        {
            set
            {
                _config = value;
                _logFile = CheckLogFile(_config.LogFile) ? _config.LogFile : string.Empty;
                _logDebug = _config.LogDebug;
                Open();
            }
        }

        public static bool IsConsole { get; set; }
        private static CancellationTokenSource _taskWriterCancellationToken => CustomTaskManager.CancellationTokenSource;

        static Log()
        {
            JsonErrorHandler += Log_JsonErrorHandler;

            _jsonSerializerSettings = new JsonSerializerSettings()
            {
                Error = JsonErrorHandler
            };

            IsConsole = true;
            Open();
        }

        public static void Close()
        {
            try
            {
                if (_isRunning)
                {
                    _messageList.AddRange(_lastMessageList);
                    _lastMessageList.Clear();

                    if (_taskWriterCancellationToken.IsCancellationRequested)
                    {
                        _taskWriter.Wait();
                    }
                    else if (_messageList.Any())
                    {
                        _taskWriter.Wait(Constants.ThreadSleepMs1000);
                    }

                    _isRunning = false;
                }
            }
            catch (TaskCanceledException) { }
            catch (AggregateException e)
            {
                if (!e.InnerExceptions.Any(x => x.GetType() == typeof(TaskCanceledException)))
                {
                    throw new AggregateException(e);
                }
            }
        }

        public static void Debug(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            if (_logDebug >= LoggingLevel.Verbose)
            {
                QueueMessage(false, new LogMessage()
                {
                    TimeStamp = DateTime.Now.ToString("o") + "::",
                    Message = $"{Thread.CurrentThread.ManagedThreadId}:{callerName}:debug:{message}{serializeJson(jsonSerializer)}",
                    LogFileOnly = true
                });
            }
        }

        public static void Error(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            if (_logDebug >= LoggingLevel.Error)
            {
                Process("error: " + message, ConsoleColor.Red, ConsoleColor.Black, jsonSerializer, isError: true, callerName: callerName);
            }
        }

        public static void Exception(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            if (_logDebug >= LoggingLevel.Exception)
            {
                Process("exception: " + message, ConsoleColor.Black, ConsoleColor.Yellow, jsonSerializer, isError: true, callerName: callerName);
            }
        }

        public static void Highlight(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            if (_logDebug >= LoggingLevel.Warning)
            {
                ConsoleColor color = ConsoleColor.White;

                if (Regex.IsMatch(message, "succeed|success|info", RegexOptions.IgnoreCase))
                {
                    color = ConsoleColor.Green;
                }

                if (Regex.IsMatch(message, "fail|error|critical", RegexOptions.IgnoreCase))
                {
                    color = ConsoleColor.Red;
                }

                if (Regex.IsMatch(message, "exception|warn|terminate", RegexOptions.IgnoreCase))
                {
                    color = ConsoleColor.Yellow;
                }

                Process(message, color, null, jsonSerializer, callerName: callerName);
            }
        }

        public static void Info(string message,
                                        ConsoleColor? foregroundColor = null,
                                        ConsoleColor? backgroundColor = null,
                                        object jsonSerializer = null,
                                        bool minimal = false,
                                        bool lastMessage = false,
                                        bool isError = false,
                                        [CallerMemberName] string callerName = "")
        {
            if (_logDebug >= LoggingLevel.Info)
            {
                Process(message, foregroundColor, backgroundColor, jsonSerializer, minimal, lastMessage, isError, callerName);
            }
        }

        public static void Info(string message, object jsonSerializer, [CallerMemberName] string callerName = "")
        {
            Info(message, null, null, jsonSerializer, callerName: callerName);
        }

        public static void Last(string message,
                                ConsoleColor? foregroundColor = null,
                                ConsoleColor? backgroundColor = null,
                                object jsonSerializer = null,
                                [CallerMemberName] string callerName = "")
        {
            Process(message, foregroundColor, backgroundColor, jsonSerializer, false, true, callerName: callerName);
        }

        public static void Min(string message,
                                ConsoleColor? foregroundColor = null,
                                ConsoleColor? backgroundColor = null,
                                object jsonSerializer = null,
                                [CallerMemberName] string callerName = "")
        {
            if (_logDebug >= LoggingLevel.Info)
            {
                Process(message, foregroundColor, backgroundColor, jsonSerializer, true, callerName: callerName);
            }
        }

        public static void Open()
        {
            if (!_isRunning)
            {
                _taskWriter = new Task(TaskWriter, _taskWriterCancellationToken.Token);
                _taskWriter.Start();
                _isRunning = true;
            }
        }

        public static void Warning(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            if (_logDebug >= LoggingLevel.Warning)
            {
                Process("warning: " + message, ConsoleColor.Yellow, ConsoleColor.Black, jsonSerializer, callerName: callerName);
            }
        }

        private static bool CheckLogFile(string logFile)
        {
            try
            {
                if (string.IsNullOrEmpty(logFile))
                {
                    _logFileEnabled = false;
                    return true;
                }

                if (!_logFileEnabled)
                {
                    string directoryName = Path.GetDirectoryName(logFile);
                    if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }

                    File.Create(logFile).Close();
                    _logFileEnabled = true;
                }

                return true;
            }
            catch (Exception e)
            {
                _logFileEnabled = false;
                Exception(e.ToString());
                return false;
            }
        }

        private static void CloseFile()
        {
            _streamWriter?.Flush();
            _streamWriter?.Close();
            _streamWriter = null;
        }

        private static void Log_JsonErrorHandler(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            e.ErrorContext.Handled = true;

            if (_logDebug >= LoggingLevel.Verbose)
            {
                Process($"json serialization error: {e.ErrorContext.OriginalObject} {e.ErrorContext.Path}");
            }
        }

        private static void Process(string message,
                                ConsoleColor? foregroundColor = null,
                                ConsoleColor? backgroundColor = null,
                                object jsonSerializer = null,
                                bool minimal = false,
                                bool lastMessage = false,
                                bool isError = false,
                                [CallerMemberName] string callerName = "")
        {
            if (!IsConsole & MessageLogged == null)
            {
                return;
            }

            if (!minimal)
            {
                message = $"{Thread.CurrentThread.ManagedThreadId}:{callerName}:{message}{serializeJson(jsonSerializer)}";
            }

            QueueMessage(lastMessage, new LogMessage()
            {
                TimeStamp = DateTime.Now.ToString("o") + "::",
                Message = message,
                BackgroundColor = backgroundColor,
                ForegroundColor = foregroundColor,
                IsError = isError
            });
        }

        private static void QueueMessage(bool lastMessage, LogMessage logMessage)
        {
            if (lastMessage)
            {
                _lastMessageList.Add(logMessage);
            }
            else
            {
                _messageList.Add(logMessage);
            }
        }

        private static void ResetColor(ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null)
        {
            if (foregroundColor != null | backgroundColor != null)
            {
                Console.ResetColor();
            }
        }

        private static string serializeJson(object jsonSerializer)
        {
            if (jsonSerializer != null)
            {
                try
                {
                    return Environment.NewLine + JsonConvert.SerializeObject(jsonSerializer, Formatting.Indented, _jsonSerializerSettings);
                }
                catch (Exception e)
                {
                    return Environment.NewLine + $"LOG:jsondeserialize error: {e.Message}";
                }
            }

            return string.Empty;
        }

        private static void SetColor(ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null)
        {
            if (foregroundColor != null)
            {
                Console.ForegroundColor = (ConsoleColor)foregroundColor;
            }

            if (backgroundColor != null)
            {
                Console.BackgroundColor = (ConsoleColor)backgroundColor;
            }
        }

        private static void TaskWriter()
        {
            while (!_taskWriterCancellationToken.IsCancellationRequested
                || (_taskWriterCancellationToken.IsCancellationRequested & _messageList.Any()))
            {
                while (_messageList.Any() & _isRunning)
                {
                    foreach (LogMessage result in _messageList.DeListAll())
                    {
                        if (!result.LogFileOnly)
                        {
                            WriteMessage(result);
                        }

                        if (_logFileEnabled)
                        {
                            WriteFile(result);
                        }
                    }
                }

                if (_isRunning)
                {
                    Thread.Sleep(_threadSleepMs);
                }
                else
                {
                    _messageList.Clear();
                    break;
                }
            }

            CloseFile();
        }

        public static void Trivial(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            if (_logDebug >= LoggingLevel.Trivial)
            {
                Debug(message, jsonSerializer, callerName);
            }
        }

        private static void WriteFile(LogMessage result)
        {
            if (_streamWriter == null)
            {
                _streamWriter = new StreamWriter(_logFile, true);
            }

            _streamWriter.WriteLine(result.TimeStamp + result.Message);
        }

        private static void WriteMessage(LogMessage message)
        {
            if (IsConsole)
            {
                System.Diagnostics.Debug.Print(message.Message);
                SetColor(message.ForegroundColor, message.BackgroundColor);

                if (message.IsError)
                {
                    LogErrors++;
                    Console.Error.WriteLine(message.Message);
                }
                else
                {
                    Console.WriteLine(message.Message);
                }

                ResetColor(message.ForegroundColor, message.BackgroundColor);
            }

            LogMessageHandler logMessage = MessageLogged;
            logMessage?.Invoke(null, message);
        }
    }
}