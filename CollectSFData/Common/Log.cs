// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CollectSFData
{
    public static class Log
    {
        private static readonly ConsoleColor _highlightBackground = Console.ForegroundColor;
        private static readonly ConsoleColor _highlightForeground = Console.BackgroundColor;
        private static readonly SynchronizedList<MessageObject> _lastMessageList = new SynchronizedList<MessageObject>();
        private static readonly SynchronizedList<MessageObject> _messageList = new SynchronizedList<MessageObject>();
        private static readonly Task _taskWriter;
        private static readonly CancellationTokenSource _taskWriterCancellationToken = new CancellationTokenSource();
        private static bool _displayingProgress;
        private static string _logFile;
        private static StreamWriter _streamWriter;
        private static int _threadSleepMs = Constants.ThreadSleepMs100;

        static Log()
        {
            _taskWriter = new Task(TaskWriter, _taskWriterCancellationToken.Token);
            _taskWriter.Start();
        }

        public static bool LogDebugEnabled { get; set; }

        public static string LogFile { get => _logFile; set => _logFile = CheckLogFile(value) ? value : string.Empty; }

        public static bool LogFileEnabled => !string.IsNullOrEmpty(LogFile);

        public static void AutoColor(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            ConsoleColor color = ConsoleColor.White;

            if (Regex.IsMatch(message, "succeed|success|true", RegexOptions.IgnoreCase))
            {
                color = ConsoleColor.Green;
            }

            if (Regex.IsMatch(message, "fail|error|false", RegexOptions.IgnoreCase))
            {
                color = ConsoleColor.Red;
            }

            if (Regex.IsMatch(message, "exception|warn|terminate", RegexOptions.IgnoreCase))
            {
                color = ConsoleColor.Yellow;
            }

            Info(message, color, null, jsonSerializer, callerName: callerName);
        }

        public static void Close()
        {
            _messageList.AddRange(_lastMessageList);
            _taskWriterCancellationToken.Cancel();
            _taskWriter.Wait();
        }

        public static void Debug(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            if (LogDebugEnabled)
            {
                Info("debug: " + message, ConsoleColor.Black, ConsoleColor.Gray, jsonSerializer, callerName: callerName);
            }
        }

        public static void Error(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            Info("error: " + message, ConsoleColor.Red, ConsoleColor.Black, jsonSerializer, callerName: callerName);
        }

        public static void Exception(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            Info("exception: " + message, ConsoleColor.Black, ConsoleColor.Yellow, jsonSerializer, callerName: callerName);
        }

        public static void Highlight(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            Info(message, _highlightForeground, _highlightBackground, jsonSerializer, callerName: callerName);
        }

        public static void Info(string message,
                                ConsoleColor? foregroundColor = null,
                                ConsoleColor? backgroundColor = null,
                                object jsonSerializer = null,
                                bool minimal = false,
                                bool lastMessage = false,
                                [CallerMemberName] string callerName = "")
        {
            if (jsonSerializer != null)
            {
                try
                {
                    jsonSerializer = Environment.NewLine + JsonConvert.SerializeObject(jsonSerializer, Formatting.Indented);
                }
                catch (Exception e)
                {
                    message += Environment.NewLine + $"LOG:jsondeserialize error{e.Message}";
                }
            }

            if (!minimal)
            {
                message = $"{Thread.CurrentThread.ManagedThreadId}:{callerName}:{message}{jsonSerializer}";
            }

            if (lastMessage)
            {
                _lastMessageList.Add(new MessageObject()
                {
                    TimeStamp = DateTime.Now.ToString("o") + "::",
                    Message = message,
                    BackgroundColor = backgroundColor,
                    ForegroundColor = foregroundColor
                });
            }
            else
            {
                _messageList.Add(new MessageObject()
                {
                    TimeStamp = DateTime.Now.ToString("o") + "::",
                    Message = message,
                    BackgroundColor = backgroundColor,
                    ForegroundColor = foregroundColor
                });
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
            Info(message, foregroundColor, backgroundColor, jsonSerializer, false, true, callerName);
        }

        public static void Min(string message,
                                ConsoleColor? foregroundColor = null,
                                ConsoleColor? backgroundColor = null,
                                object jsonSerializer = null,
                                [CallerMemberName] string callerName = "")
        {
            Info(message, foregroundColor, backgroundColor, jsonSerializer, true, callerName: callerName);
        }

        public static void Warning(string message, object jsonSerializer = null, [CallerMemberName] string callerName = "")
        {
            Info("warning: " + message, ConsoleColor.Yellow, ConsoleColor.Black, jsonSerializer, callerName: callerName);
        }

        private static bool CheckLogFile(string logFile)
        {
            try
            {
                if (string.IsNullOrEmpty(logFile))
                {
                    Close();
                    return true;
                }

                if (!Directory.Exists(Path.GetDirectoryName(logFile)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logFile));
                }

                File.Create(logFile).Close();
                return true;
            }
            catch (Exception e)
            {
                _taskWriterCancellationToken.Cancel();
                Exception(e.ToString());
                return false;
            }
        }

        private static void CloseFile()
        {
            if (_streamWriter != null)
            {
                _streamWriter.Flush();
                _streamWriter.Close();
                _streamWriter = null;
            }
        }

        private static void ResetColor(ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null)
        {
            if (foregroundColor != null | backgroundColor != null)
            {
                Console.ResetColor();
            }
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
                while (_messageList.Any())
                {
                    foreach (MessageObject result in _messageList.DeListAll())
                    {
                        WriteMessage(result);
                        if (LogFileEnabled)
                        {
                            WriteFile(result);
                        }
                    }
                }

                Thread.Sleep(_threadSleepMs);
            }

            CloseFile();
        }

        private static void WriteFile(MessageObject result)
        {
            if (_streamWriter == null)
            {
                _streamWriter = new StreamWriter(LogFile, true);
            }

            _streamWriter.WriteLine(result.TimeStamp + result.Message);
        }

        private static void WriteMessage(MessageObject message)
        {
            System.Diagnostics.Debug.Print(message.Message);
            SetColor(message.ForegroundColor, message.BackgroundColor);
            if (_displayingProgress)
            {
                _displayingProgress = false;
                Console.WriteLine(Environment.NewLine);
            }

            Console.WriteLine(message.Message);
            ResetColor(message.ForegroundColor, message.BackgroundColor);
        }

        public class MessageObject
        {
            public ConsoleColor? BackgroundColor { get; set; }

            public ConsoleColor? ForegroundColor { get; set; }

            public string Message { get; set; }

            public string TimeStamp { get; set; }
        }
    }
}