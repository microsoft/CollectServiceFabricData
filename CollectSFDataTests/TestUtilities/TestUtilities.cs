﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

//https://www.automatetheplanet.com/nunit-cheat-sheet/
//https://github.com/nunit/docs/wiki/Tips-And-Tricks
//https://github.com/nunit/nunit-csharp-samples

using CollectSFData.Azure;
using CollectSFData.Common;
using CollectSFData.DataFile;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;

namespace CollectSFDataTests
{
    public class ProcessOutput
    {
        public int ExitCode { get; set; } = 0;

        public string StandardError { get; set; } = "";

        public string StandardOutput { get; set; } = "";

        public bool HasErrors()
        {
            return !string.IsNullOrEmpty(StandardError) | ExitCode > 0;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    [TestFixture]
    public class TestUtilities
    {
        public StringBuilder consoleErrBuilder = new StringBuilder();
        public StringBuilder consoleOutBuilder = new StringBuilder();
        public string[] TempArgs;
        private static object _executing = new object();

        static TestUtilities()
        {
            OneTimeSetup();
        }

        public TestUtilities()
        {
            Log.Info("enter");
            Directory.SetCurrentDirectory(TempDir);

            File.Copy(TestOptionsFile, TempOptionsFile, true);

            ConfigurationOptions = new ConfigurationOptions();

            //TempArgs = new string[2] { "-config", TestOptionsFile };
            ConfigurationOptions.PopulateConfig(TestArgs);
            //ConfigurationOptions.CacheLocation = "";// null;
            SaveTempOptions();

            TempArgs = new string[2] { "-config", TempOptionsFile };
        }

        public static TestContext Context { get; set; }

        //public static string DefaultOptionsFile => $"{WorkingDir}\\..\\..\\..\\..\\configurationFiles\\collectsfdata.options.json";
        public static string TempDir => $"{WorkingDir}\\..\\..\\Temp";

        public static string[] TestArgs => new string[2] { "-config", TestOptionsFile };
        public static string TestConfigurationsDir => $"{WorkingDir}\\..\\..\\..\\TestConfigurations";

        public static TestProperties TestProperties { get; set; }

        public static string TestPropertiesFile => $"{Environment.GetEnvironmentVariable("LOCALAPPDATA")}\\collectsfdata\\collectSfDataTestProperties.json";

        public static string TestPropertiesSetupScript => $"{TestUtilitiesDir}\\setup-test-env.ps1";

        public static string TestUtilitiesDir => $"{WorkingDir}\\..\\..\\..\\TestUtilities";

        public static string WorkingDir { get; set; } = string.Empty;

        public ConfigurationOptions ConfigurationOptions { get; set; } //= new ConfigurationOptions();

        public string TempOptionsFile { get; private set; } = $"{TempDir}\\collectsfdata.options.json";

        private static string TestOptionsFile => $"{TestConfigurationsDir}\\collectsfdata.options.json";

        private StringWriter ConsoleErr { get; set; } = new StringWriter();

        private StringWriter ConsoleOut { get; set; } = new StringWriter();

        public static Collection<PSObject> ExecutePowerShellCommand(string command)
        {
            RunspaceConfiguration runspaceConfiguration = RunspaceConfiguration.Create();
            Runspace runspace = RunspaceFactory.CreateRunspace(runspaceConfiguration);
            runspace.Open();

            Pipeline pipeline = runspace.CreatePipeline();
            pipeline.Commands.Add(new Command(command));

            Collection<PSObject> results = pipeline.Invoke();
            return results;
        }

        public static void Main(string[] args)
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

            ReadTestSettings();
        }

        public ProcessOutput ExecuteCollectSfData(string arguments = null, bool withTempConfig = true, bool wait = true)
        {
            Log.Info("enter");
            if (withTempConfig)
            {
                arguments += $" -config {TempOptionsFile}";
            }

            return ExecuteProcess("collectsfdata.exe", arguments, wait);
        }

        public ProcessOutput ExecuteMoqTest(ConfigurationOptions options = null)
        {
            lock (_executing)
            {
                Log.Info("enter");

                SaveTempOptions();
                //Program.Config = new ConfigurationOptions();
                //Program program = new Program();
                var program = new Mock<Program>();
                //Moq.Language.Flow.ISetup<Program, int> result = program.Setup(p => p.Execute(TempArgs));
                program.Setup(p => p.Execute(TempArgs));

                Assert.IsNotNull(program);

                StartConsoleRedirection();
                Log.Info(">>>>Starting test<<<<\r\n", ConfigurationOptions);
                //int result = program.Execute(TempArgs);
                //Log.Info(">>>>test result<<<<", result);
                ProcessOutput output = StopConsoleRedirection();

                Assert.IsNotNull(output);
                /*
                if (result. != 0)
                {
                    Log.Error($"result {result}");
                    output.ExitCode = result;
                }
                */
                //Log.Info(">>>>test result<<<<", output);
                return output;
            }
        }

        public ProcessOutput ExecuteProcess(string imageFile, string arguments = null, bool wait = true)
        {
            Log.Info($"ExecuteProcess: current dir: {Directory.GetCurrentDirectory()} image: {imageFile} args: {arguments}");
            Assert.IsTrue(File.Exists(imageFile));

            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo(imageFile, arguments);
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = !wait;
            startInfo.RedirectStandardOutput = wait;
            startInfo.RedirectStandardError = wait;
            startInfo.LoadUserProfile = false;
            startInfo.ErrorDialog = false;

            process.StartInfo = startInfo;
            bool reference = process.Start();
            ProcessOutput output = new ProcessOutput();

            if (wait && reference && !process.HasExited)
            {
                process.WaitForExit();
            }

            while (process.StandardOutput.Peek() > -1)
            {
                string line = process.StandardOutput.ReadLine();
                TestContext.WriteLine(line);
                output.StandardOutput += line;
            }

            while (process.StandardError.Peek() > -1)
            {
                string errorLine = $"error:{process.StandardError.ReadLine()}";
                Console.Error.WriteLine(errorLine);
                output.StandardError += errorLine;
            }

            output.ExitCode = process.ExitCode;
            return output;
        }

        public ProcessOutput ExecuteTest(ConfigurationOptions options = null)
        {
            lock (_executing)
            {
                Log.Close();
                Log.Start();
                Log.Info("enter");

                SaveTempOptions();
                //Instance.Config = new ConfigurationOptions();
                Program program = new Program();
                Assert.IsNotNull(program);

                StartConsoleRedirection();
                Log.Info(">>>>Starting test<<<<\r\n", ConfigurationOptions);
                // cant call with args
                //
                // populate default collectsfdata.options.json
                //int result = program.Execute(TempArgs);
                int result = program.Execute(new string[] { });
                Log.Info(">>>>test result<<<<", result);
                ProcessOutput output = StopConsoleRedirection();

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

        public void FlushConsoleOutput()
        {
            Console.Out.Flush();
            Console.Error.Flush();
            ConsoleOut.Flush();
            ConsoleErr.Flush();
        }

        public ProcessOutput GetConsoleOutput()
        {
            ProcessOutput output = StopConsoleRedirection();
            WriteConsole(ConsoleOut.ToString());
            Console.Error.WriteLine(ConsoleErr.ToString());
            StartConsoleRedirection();
            return output;
        }

        [SetUp]
        public void Setup()
        {
            Instance.Config = new ConfigurationOptions();
            Instance.FileMgr = new FileManager();
            WriteConsole("TestContext", Context);
            TestContext.WriteLine($"starting test: {Context?.Test.Name}");
            consoleOutBuilder = new StringBuilder();
            consoleErrBuilder = new StringBuilder();
        }

        public void StartConsoleRedirection()
        {
            Log.Start();
            Log.LogErrors = 0;
            Log.Info("starting redirection");
            FlushConsoleOutput();
            Console.SetOut(ConsoleOut);
            Console.SetError(ConsoleErr);
        }

        public ProcessOutput StopConsoleRedirection()
        {
            Log.Info("stopping redirection");
            // give time for logging to finish
            Thread.Sleep(1000);
            Log.Close();
            FlushConsoleOutput();
            ProcessOutput output = new ProcessOutput
            {
                StandardError = ConsoleErr.ToString(),
                StandardOutput = ConsoleOut.ToString()
            };

            ConsoleErr = new StringWriter();
            ConsoleOut = new StringWriter();

            Console.SetOut(Console.Out);
            Console.SetError(Console.Error);

            consoleOutBuilder.Append(output.StandardOutput);
            consoleErrBuilder.Append(output.StandardError);

            return output;
        }

        [TearDown]
        public void TearDown()
        {
            WriteConsole($"standard out:\r\n{consoleOutBuilder}");
            WriteConsole($"standard err:\r\n{consoleErrBuilder}");
            WriteConsole($"finished test: {Context?.Test.Name}", Context);
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

        private void SaveTempOptions()
        {
            ConfigurationOptions.SaveConfiguration = TempOptionsFile;
            ConfigurationOptions.SaveConfigFile();
        }
    }
}