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

namespace CollectSFDataTests
{
    public static class TestUtilities
    {
        public static string[] TestArgs = new string[1] { TestOptionsFile };
        public static ConfigurationOptionsTests TestConfig = new ConfigurationOptionsTests();
        public static string TestConfigurationsDir = "..\\..\\..\\TestConfigurations";
        public static string TestFilesDir = "..\\..\\..\\TestFiles";
        public static string TestOptionsFile = $"{TestConfigurationsDir}\\collectsfdata.options.json";
        public static string TestPropertiesFile = $".\\testproperties.json";

        static TestUtilities()
        {
            Assert.IsTrue(File.Exists(TestOptionsFile));
            Assert.IsTrue(Directory.Exists(TestFilesDir));
        }

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
    }
}