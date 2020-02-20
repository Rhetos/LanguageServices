using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NLog;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    [DeploymentItem("RhetosAppFolder\\", "RhetosAppFolder")]
    public class RhetosWorkspaceTests
    {
        private readonly IServiceProvider serviceProvider;

        public RhetosWorkspaceTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            serviceProvider = TestCommon.CreateTestServiceProvider();
            serviceProvider.GetService<RhetosAppContext>().InitializeFromCurrentDomain();
        }

        [DataTestMethod]
        [DataRow(@"RhetosAppFolder\FolderWithApp\SubFolder\doc.rhe", @"RhetosAppFolder\FolderWithApp", RootPathConfigurationType.DetectedRhetosApp)]
        [DataRow(@"RhetosAppFolder\FolderWithApp\doc.rhe", @"RhetosAppFolder\FolderWithApp", RootPathConfigurationType.DetectedRhetosApp)]
        [DataRow(@"RhetosAppFolder\EmptyFolder\doc.rhe", null, null)]
        [DataRow(@"RhetosAppFolder\EmptyFolder\directive.rhe", @"C:\SomeFolder\SomeSubFolder\MyRhetosAppFolder\", RootPathConfigurationType.SourceDirective)]
        public void RhetoRootAppPathFromDocument(string documentRelativePath, string expectedRootAppPath, RootPathConfigurationType? expectedConfigurationType)
        {
            var uri = new Uri(Path.Combine(Environment.CurrentDirectory, documentRelativePath));
            Console.WriteLine($"Opening file '{uri.LocalPath}.'");
            var text = File.ReadAllText(uri.LocalPath);
            Console.WriteLine($"File contents:\n{text}\n*****************\n");
            var rhetosWorkspace =  serviceProvider.GetRequiredService<RhetosWorkspace>();
            rhetosWorkspace.UpdateDocumentText(uri, text);

            var rootPathConfiguration = rhetosWorkspace.GetRhetosAppRootPath(uri);
            Console.WriteLine($"\nRoot Path Configuration:\n{rootPathConfiguration?.ConfigurationType} = {rootPathConfiguration?.RootPath}\n  from {rootPathConfiguration?.Source}");

            if (string.IsNullOrEmpty(expectedRootAppPath))
            {
                Assert.IsNull(rootPathConfiguration);
                return;
            }

            Assert.AreEqual(expectedConfigurationType, rootPathConfiguration.ConfigurationType);
            if (rootPathConfiguration.ConfigurationType != RootPathConfigurationType.SourceDirective)
                expectedRootAppPath = Path.Combine(Environment.CurrentDirectory, expectedRootAppPath);
            
            Assert.AreEqual(expectedRootAppPath, rootPathConfiguration.RootPath);
        }
    }
}
