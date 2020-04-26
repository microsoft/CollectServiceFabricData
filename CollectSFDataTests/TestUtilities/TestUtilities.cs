// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

//https://www.automatetheplanet.com/nunit-cheat-sheet/
//https://github.com/nunit/docs/wiki/Tips-And-Tricks
//https://github.com/nunit/nunit-csharp-samples

using CollectSFData;

using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

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
        public static string[] TestArgs = new string[2] { "-config", TestOptionsFile };
        public string[] TempArgs;
        private static object _executing = new object();

        static TestUtilities()
        {
            SetupTests();
        }

        public TestUtilities()
        {
            PopulateTempOptions();
        }

        public static TestContext Context { get; set; }

        public static string DefaultOptionsFile => $"{WorkingDir}\\..\\..\\..\\..\\configurationFiles\\collectsfdata.options.json";

        public static string TempDir => $"{WorkingDir}\\..\\..\\Temp";

        public static string TestConfigurationsDir => $"{WorkingDir}\\..\\..\\..\\TestConfigurations";

        public static string TestFilesDir => $"{WorkingDir}\\..\\..\\..\\TestFiles";

        public static TestProperties TestProperties { get; set; }

        public static string TestPropertiesFile => $"{TempDir}\\collectSfDataTestProperties.json";

        public static string TestPropertiesSetupScript => $"{TestUtilitiesDir}\\setup-test-env.ps1";

        public static string TestUtilitiesDir => $"{WorkingDir}\\..\\..\\..\\TestUtilities";

        public static string WorkingDir { get; set; } = string.Empty;

        public ConfigurationOptions ConfigurationOptions { get; set; } = new ConfigurationOptions();

        public string TempOptionsFile { get; private set; } = $"{TempDir}\\collectsfdatda.{Guid.NewGuid()}.json";

        //public TestContext TestContext { get; set; }
        private static string TestOptionsFile => $"{TestConfigurationsDir}\\collectsfdata.options.json";

        private StringWriter ConsoleErr { get; set; } = new StringWriter();

        private StringWriter ConsoleOut { get; set; } = new StringWriter();

        public static bool BuildWindowsCluster()
        {
            string templateFile = $".\\sf-1nt-3n-1lb.json";
            string templateParameterFile = $".\\sf-1nt-3n-1lb.parameters.json";

            var results = ExecutePowerShellCommand($".\\azure-az-deploy-template.ps1 -force" +
                $" -templatefile '{templateFile}'" +
                $" -templateParameterFile '{templateParameterFile}'");
            return true;
        }

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
        public static void SetupTests()
        {
            Context = TestContext.CurrentContext;
            WorkingDir = Context?.WorkDirectory ?? Directory.GetCurrentDirectory();
            //ClassTestContext = context;
            Assert.IsTrue(File.Exists(TestOptionsFile));
            Assert.IsTrue(Directory.Exists(TestFilesDir));

            if (!Directory.Exists(TempDir))
            {
                Directory.CreateDirectory(TempDir);
            }

            if (!File.Exists(TestPropertiesFile))
            {
                Collection<PSObject> result = ExecutePowerShellCommand(TestPropertiesSetupScript);
            }

            Assert.IsTrue(File.Exists(TestPropertiesFile));

            TestProperties = JsonConvert.DeserializeObject<TestProperties>(File.ReadAllText(TestPropertiesFile));
            Assert.IsNotNull(TestProperties);
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

        public ProcessOutput ExecuteTest()
        {
            lock (_executing)
            {
                Log.Info("enter");

                SaveTempOptions();
                Program program = new Program();
                Assert.IsNotNull(program);

                StartConsoleRedirection();
                int result = program.Execute(TempArgs);

                ProcessOutput output = StopConsoleRedirection();

                Assert.IsNotNull(output);

                if (result != 0)
                {
                    Log.Error($"result {result}");
                }

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
            WriteConsole("TestContext", Context);
            TestContext.WriteLine($"starting test: {Context?.Test.Name}");
        }

        public void StartConsoleRedirection()
        {
            FlushConsoleOutput();
            Console.SetOut(ConsoleOut);
            Console.SetError(ConsoleErr);
        }

        public ProcessOutput StopConsoleRedirection()
        {
            Log.Close();
            FlushConsoleOutput();
            ProcessOutput output = new ProcessOutput
            {
                StandardError = ConsoleErr.ToString(),
                StandardOutput = ConsoleOut.ToString()
            };

            Console.SetOut(Console.Out);
            Console.SetError(Console.Error);
            return output;
        }

        [TearDown]
        public void TearDown()
        {
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

        private void PopulateTempOptions()
        {
            Log.Info("enter");

            ConfigurationOptions = new ConfigurationOptions();

            TempArgs = new string[2] { "-config", TestOptionsFile };
            ConfigurationOptions.PopulateConfig(TestArgs);
            SaveTempOptions();

            TempArgs = new string[2] { "-config", TempOptionsFile };
        }

        private void SaveTempOptions()
        {
            ConfigurationOptions.SaveConfiguration = TempOptionsFile;
            ConfigurationOptions.SaveConfigFile();
        }
    }
}