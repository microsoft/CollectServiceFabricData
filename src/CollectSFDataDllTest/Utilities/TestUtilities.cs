using CollectSFData;
using CollectSFData.Azure;
using CollectSFData.Common;
using CollectSFDataTest.Utilities;

//using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;

namespace CollectSFDataDllTest.Utilities
{
    [TestFixture]
    public class TestUtilities
    {
        public Collector Collector;
        public List<string> LogMessageQueue = new List<string>();
        public string[] TempArgs;
        private static object _executing = new object();
        private bool _logMessageQueueEnabled;
        private bool hasExited = false;
        public static TestContext Context { get; set; }
        public static string DefaultOptionsFile => $"{WorkingDir}\\..\\..\\..\\..\\..\\..\\configurationFiles\\collectsfdata.options.json";
        public static string ScriptsDir => $"{WorkingDir}\\..\\..\\..\\..\\..\\..\\scripts";
        public static string SolutionDir => $"{WorkingDir}\\..\\..\\..\\..\\..\\..";
        public static string TempDir => $"{WorkingDir}\\..\\..\\Temp";
        public static string TestConfigurationsDir => $"{WorkingDir}\\..\\..\\..\\..\\TestConfigurations";
        public static string TestDataFilesDir => $"{WorkingDir}\\..\\..\\..\\..\\TestDataFiles";
        public static TestProperties TestProperties { get; set; }
        public static string TestPropertiesFile => $"{Environment.GetEnvironmentVariable("LOCALAPPDATA")}\\collectsfdata\\collectSfDataTestProperties.json";
        public static string TestPropertiesSetupScript => $"{ScriptsDir}\\setup-test-env.ps1";
        public static string WorkingDir { get; set; } = string.Empty;
        public ConfigurationOptions ConfigurationOptions { get; set; }

        public bool LogMessageQueueEnabled
        {
            get
            {
                return _logMessageQueueEnabled;
            }

            set
            {
                if (_logMessageQueueEnabled != value)
                {
                    _logMessageQueueEnabled = value;

                    if (value)
                    {
                        Log.MessageLogged += Log_MessageLogged;
                    }
                    else
                    {
                        Log.MessageLogged -= Log_MessageLogged;
                    }
                }
            }
        }

        public string TempOptionsFile { get; private set; } = $"{TempDir}\\collectsfdata.options.json";
        private static string TestOptionsFile => $"{TestConfigurationsDir}\\collectsfdata.options.json";

        static TestUtilities()
        {
            OneTimeSetup();
        }

        public TestUtilities()
        {
            Log.Info("enter");
            Collector = new Collector();
            Directory.SetCurrentDirectory(TempDir);

            File.Copy(TestOptionsFile, TempOptionsFile, true);

            ConfigurationOptions = new ConfigurationOptions();

            SaveTempOptions();

            TempArgs = new string[2] { "-config", TempOptionsFile };
        }

        public static Collection<PSObject> ExecutePowerShellCommand(string command)
        {
            Collection<PSObject> results = new Collection<PSObject>();

            try
            {
                //RunspaceConfiguration runspaceConfiguration = RunspaceConfiguration.Create();

                InitialSessionState initialSessionState = InitialSessionState.CreateDefault();
                initialSessionState.AuthorizationManager = new AuthorizationManager("csfdtest");
                Runspace runspace = RunspaceFactory.CreateRunspace(initialSessionState); // (runspaceConfiguration);
                //runspace.ConnectionInfo.Credential = new PSCredential(new PSObject())
                //runspace.ConnectionInfo.SetSessionOptions(new System.Management.Automation.Remoting.PSSessionOption() { })
                runspace.Open();

                Pipeline pipeline = runspace.CreatePipeline();
                pipeline.Commands.Add(new Command(command));

                results = pipeline.Invoke();
                return results;
            }
            catch (Exception e)
            {
                Log.Exception($"{e}", e);
                return results;
            }
        }

        public static void Main()
        {
            TestUtilities testUtilities = new TestUtilities();
            testUtilities.WriteConsole("", testUtilities.TempArgs);
        }

        [OneTimeSetUp]
        public static void OneTimeSetup()
        {
            Context = TestContext.CurrentContext;
            WorkingDir = Context?.WorkDirectory ?? Directory.GetCurrentDirectory();
            Assert.IsTrue(File.Exists(TestOptionsFile));

            if (!Directory.Exists(TempDir))
            {
                Directory.CreateDirectory(TempDir);
            }

            //            ReadTestSettings();
        }

        [OneTimeTearDown]
        public static void OneTimeTearDown()
        {
            //CustomTaskManager.Close();
            Log.Close();
        }

        public ProcessOutput AfterTest()
        {
            Log.Info("after test");
            // give time for logging to finish
            Thread.Sleep(1000);
            Log.Close();
            //FlushConsoleOutput();
            ProcessOutput output = new ProcessOutput
            {
                //    StandardError = ConsoleErr.ToString(),
                //    StandardOutput = ConsoleOut.ToString()
            };

            //ConsoleErr = new StringWriter();
            //ConsoleOut = new StringWriter();

            //Console.SetOut(Console.Out);
            //Console.SetError(Console.Error);

            //consoleOutBuilder.Append(output.StandardOutput);
            //consoleErrBuilder.Append(output.StandardError);

            return output;
        }

        public void BeforeTest()
        {
            Log.Open();
            Log.LogErrors = 0;
            Log.Info("starting redirection");
            //FlushConsoleOutput();
            //Console.SetOut(ConsoleOut);
            //Console.SetError(ConsoleErr);
        }

        public ProcessOutput ExecuteCollectSfData(string arguments = null, bool wait = true)
        {
            Log.Info("enter");

            return ExecuteProcess($"{Context.WorkDirectory}\\collectsfdata.exe", arguments, wait);
        }

        public ProcessOutput ExecuteProcess(string imageFile, string arguments = null, bool wait = true)
        {
            hasExited = false;
            Log.Info($"ExecuteProcess: current dir: {Directory.GetCurrentDirectory()} image: {imageFile} args: {arguments}");
            Assert.IsTrue(File.Exists(imageFile));

            Process process = new Process();
            process.Exited += new EventHandler(ProcessExited);
            process.EnableRaisingEvents = true;

            //ProcessStartInfo startInfo = new ProcessStartInfo($"cmd.exe", $" /c {imageFile} {arguments}");
            ProcessStartInfo startInfo = new ProcessStartInfo(imageFile, arguments);
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = !wait;
            startInfo.RedirectStandardOutput = wait;
            startInfo.RedirectStandardError = wait;
            startInfo.ErrorDialog = false;

            process.StartInfo = startInfo;
            bool reference = process.Start();
            ProcessOutput output = new ProcessOutput();

            while (!hasExited && wait && reference) // && !process.HasExited)
            {
                Thread.Sleep(100);
                //while (wait && reference && !process.HasExited)
                //process.WaitForExit();
                while (process.StandardOutput.Peek() > -1)
                {
                    string line = process.StandardOutput.ReadToEnd();//.ReadLine();
                    TestContext.WriteLine(line);
                    output.StandardOutput += line;
                }

                while (process.StandardError.Peek() > -1)
                {
                    string errorLine = $"error:{process.StandardError.ReadToEnd()}";//.ReadLine()}";
                    Console.Error.WriteLine(errorLine);
                    output.StandardError += errorLine;
                }
            }

            output.ExitCode = process.ExitCode;
            return output;
        }

        public ProcessOutput ExecuteTest(Func<bool> func)
        {
            return ExecuteTest((a) =>
            {
                Func<Func<bool>, bool> newFunc = (nf) =>
                {
                    return nf();
                };
                return newFunc(a);
            }, func);
        }

        public ProcessOutput ExecuteTest<T>(Func<T, bool> func, T input)
        {
            LogMessageQueueEnabled = false;
            Log.Close();
            Log.Open();

            LogMessageQueueEnabled = true;
            Log.Info("enter");

            BeforeTest();
            Log.Info(">>>>Starting test<<<<\r\n", ConfigurationOptions);

            bool result = func(input);

            Log.Close();
            LogMessageQueueEnabled = false;

            Log.Info(">>>>test result<<<<", result);
            ProcessOutput output = AfterTest();
            output.LogMessages = LogMessageQueue.ToList();
            LogMessageQueue.Clear();
            Assert.IsNotNull(output);

            Log.Error($"result {result}");

            output.ExitCode = Convert.ToInt32(result);
            output.ExitBool = Convert.ToBoolean(result);
            if (!output.ExitBool)
            {
                output.ExitCode = -1;
            }

            WriteConsole($">>>>test result<<<<", output);
            return output;
        }

        public ProcessOutput ExecuteTest()
        {
            lock (_executing)
            {
                Log.Close();
                Log.Open();
                Log.Info("enter");

                SaveTempOptions();
                ConfigurationOptions config = new ConfigurationOptions(TempArgs, true);
                Collector collector = new Collector();
                Assert.IsNotNull(collector);

                BeforeTest();
                Log.Info(">>>>Starting test<<<<\r\n", ConfigurationOptions);
                // cant call with args
                //
                // populate default collectsfdata.options.json
                int result = collector.Collect();
                //int result = program.Collect(new string[] { });

                Log.Info(">>>>test result<<<<", result);
                ProcessOutput output = AfterTest();

                Assert.IsNotNull(output);

                if (result != 0)
                {
                    Log.Error($"result {result}");
                    output.ExitCode = result;
                }

                WriteConsole($">>>>test result<<<<", output);
                return output;
            }
        }

        public void SaveTempOptions()
        {
            ConfigurationOptions.SaveConfiguration = TempOptionsFile;
            ConfigurationOptions.SaveConfigFile();
        }

        [SetUp]
        public void Setup()
        {
            Log.Info("enter");
            //WriteConsole("TestContext", Context);
            //TestContext.WriteLine($"starting test: {Context?.Test.Name}");
            //consoleOutBuilder = new StringBuilder();
            //consoleErrBuilder = new StringBuilder();
        }

        public void WriteConsole(string data, object json = null)
        {
            if (Context != null)
            {
                TestContext.WriteLine(data);
                if (json != null)
                {
                    TestContext.WriteLine(JsonConvert.SerializeObject(json, Formatting.Indented));
                }
            }
            else
            {
                Console.WriteLine(data);
                if (json != null)
                {
                    Console.WriteLine(JsonConvert.SerializeObject(json, Formatting.Indented));
                }
            }
        }

        private static void ReadTestSettings(bool force = false)
        {
            if (!File.Exists(TestPropertiesFile) | force)
            {
                Collection<PSObject> result = ExecutePowerShellCommand(TestPropertiesSetupScript);
            }

            Assert.IsTrue(File.Exists(TestPropertiesFile));

            TestProperties = JsonConvert.DeserializeObject<TestProperties>(File.ReadAllText(TestPropertiesFile));
            Assert.IsNotNull(TestProperties);
            string sasUri = TestProperties.SasKey;

            if (sasUri == null & !force)
            {
                ReadTestSettings(true);
            }
            else if (sasUri != null)
            {
                Console.WriteLine($"checking test sasuri {sasUri}");
                SasEndpoints endpoints = new SasEndpoints(sasUri);
                Console.WriteLine($"checking sasuri result {endpoints.IsValid()}");

                if (!endpoints.IsValid() & !force)
                {
                    ReadTestSettings(true);
                    endpoints = new SasEndpoints(TestProperties.SasKey);
                    Console.WriteLine($"checking new sasuri result {endpoints.IsValid()}");
                    Assert.AreEqual(true, endpoints.IsValid());
                }
            }
        }

        private void Log_MessageLogged(object sender, LogMessage args)
        {
            if (LogMessageQueueEnabled)
            {
                LogMessageQueue.Add(args.Message);
            }

            WriteConsole(args.Message);
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            //Log.Info($"sender", sender);
            //Log.Info($"args", e);
            hasExited = true;
        }
    }
}