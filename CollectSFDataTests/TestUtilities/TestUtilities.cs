using System.Diagnostics;
using System.Management.Automation;
using CollectSFData.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation.Runspaces;
using System.Collections;
using System.Collections.ObjectModel;
using CollectSFData;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace CollectSFDataTests
{
    public class ProcessOutput
    {
        public int ExitCode { get; set; } = 0;

        public string StandardError { get; set; } = "";
        public string StandardOutput { get; set; } = "";

        public bool HasErrors()
        {
            //return !string.IsNullOrEmpty(StandardError) | ExitCode > 0 | Regex.IsMatch(StandardOutput, "error", RegexOptions.IgnoreCase);
            return !string.IsNullOrEmpty(StandardError) | ExitCode > 0;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    public class TestUtilities
    {
        public static string[] TestArgs = new string[2] { "-config", TestOptionsFile };
        public string[] TempArgs; //= new string[2] { "config", TempOptionsFile };

        static TestUtilities()
        {
            Assert.IsTrue(File.Exists(TestOptionsFile));
            Assert.IsTrue(Directory.Exists(TestFilesDir));

            if (Directory.Exists(TempDir))
            {
                Directory.Delete(TempDir, true);
            }

            Directory.CreateDirectory(TempDir);
        }

        public TestUtilities()
        {
            //   TestOptions = new ConfigurationOptions();
            PopulateTempOptions();
            PopulateTestOptions();
        }

        public static StringWriter ConsoleErr { get; set; } = new StringWriter();
        public static StringWriter ConsoleOut { get; set; } = new StringWriter();
        public static string DefaultOptionsFile => $"..\\..\\..\\..\\configurationFiles\\collectsfdata.options.json";
        public static string TempDir => "..\\..\\Temp";
        public static string TestConfigurationsDir => "..\\..\\..\\TestConfigurations";
        public static string TestFilesDir => "..\\..\\..\\TestFiles";
        public static ConfigurationOptions TestOptions { get; set; } = new ConfigurationOptions();
        public static string TestOptionsFile => $"{TestConfigurationsDir}\\collectsfdata.options.json";
        public static string TestPropertiesFile => $".\\testproperties.json";
        public ConfigurationOptions TempOptions { get; set; } = new ConfigurationOptions();
        public string TempOptionsFile { get; set; } = $"{TempDir}\\collectsfdatda.{DateTime.Now.ToString("yyMMddhhmmssfff")}.json";

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

        public static void FlushConsoleOutput()
        {
            Console.Out.Flush();
            Console.Error.Flush();
            ConsoleOut.Flush();
            ConsoleErr.Flush();
        }

        public static ProcessOutput GetConsoleOutput()
        {
            ProcessOutput output = StopConsoleRedirection();
            Console.WriteLine(ConsoleOut.ToString());
            Console.Error.WriteLine(ConsoleErr.ToString());
            //Assert.IsFalse(string.IsNullOrEmpty(ConsoleErr.ToString()));
            StartConsoleRedirection();
            return output;
        }

        public static void StartConsoleRedirection()
        {
            FlushConsoleOutput();
            Console.SetOut(ConsoleOut);
            Console.SetError(ConsoleErr);
        }

        public static ProcessOutput StopConsoleRedirection()
        {
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
                Console.WriteLine(line);
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

        public ProcessOutput ExecuteTest(ConfigurationOptions testOptions)
        {
            Log.Info("enter");
            PopulateTestOptions(testOptions);

            StartConsoleRedirection();
            ProgramTests programTests = new ProgramTests();
            programTests.ExecuteTest();
            ProcessOutput output = StopConsoleRedirection();

            Assert.IsNotNull(output);
            return output;
        }

        public void PopulateTempOptions()
        {
            Log.Info("enter");
            TempOptions = new ConfigurationOptions();
            TempArgs = new string[2] { "-config", TestOptionsFile };
            //TempOptions.PopulateConfig(new string[1] { TempOptionsFile });
            TempOptions.PopulateConfig(TestArgs);
            TempOptions.SaveConfiguration = TempOptionsFile;
            TempOptions.SaveConfigFile();
        }

        public void PopulateTestOptions(ConfigurationOptions tempOptions = null)
        {
            Log.Info("enter");

            if (tempOptions != null)
            {
                if (string.IsNullOrEmpty(tempOptions.SaveConfiguration))
                {
                    tempOptions.SaveConfiguration = TempOptionsFile;
                }

                tempOptions.SaveConfigFile();
            }

            TestArgs = new string[2] { "-config", TempOptionsFile };
            TestOptions = new ConfigurationOptions();
            TestOptions.PopulateConfig(TestArgs);
            TestOptions.SaveConfiguration = TempOptionsFile;
            TestOptions.SaveConfigFile();
        }
    }
}