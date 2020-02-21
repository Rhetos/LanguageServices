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
    public class RhetosAppContextTests
    {
        private readonly IServiceProvider serviceProvider;

        public RhetosAppContextTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            serviceProvider = TestCommon.CreateTestServiceProvider();
            serviceProvider.GetService<RhetosAppContext>().InitializeFromCurrentDomain();
        }

        [DataTestMethod]
        [DataRow(@"RhetosAppFolder\FolderWithApp\SubFolder\doc.rhe", @"RhetosAppFolder\FolderWithApp", RootPathConfigurationType.DetectedRhetosApp)]
        [DataRow(@"RhetosAppFolder\FolderWithApp\doc.rhe", @"RhetosAppFolder\FolderWithApp", RootPathConfigurationType.DetectedRhetosApp)]
        [DataRow(@"RhetosAppFolder\EmptyFolder\doc.rhe", null, RootPathConfigurationType.None)]
        [DataRow(@"RhetosAppFolder\EmptyFolder\directive.rhe", @"C:\SomeFolder\SomeSubFolder\MyRhetosAppFolder\", RootPathConfigurationType.SourceDirective)]
        [DataRow(@"RhetosAppFolder\FolderWithConfiguration\FaultyConfiguration\doc.rhe", null, RootPathConfigurationType.None)]
        [DataRow(@"RhetosAppFolder\FolderWithConfiguration\FaultyConfiguration\FaultySubFolder\doc.rhe", null, RootPathConfigurationType.None)]
        [DataRow(@"RhetosAppFolder\FolderWithConfiguration\FaultyConfiguration\directive.rhe", @"C:\SomeFolder\SomeSubFolder\MyRhetosAppFolder\", RootPathConfigurationType.SourceDirective)]
        [DataRow(@"RhetosAppFolder\FolderWithConfiguration\doc.rhe", @"c:\FolderFromJson\Rhetos", RootPathConfigurationType.ConfigurationFile)]
        [DataRow(@"RhetosAppFolder\FolderWithConfiguration\SubFolder\doc.rhe", @"c:\FolderFromJson\Rhetos", RootPathConfigurationType.ConfigurationFile)]
        public void RhetoRootAppPathFromDocument(string documentRelativePath, string expectedRootAppPath, RootPathConfigurationType expectedConfigurationType)
        {
            var uri = new Uri(Path.Combine(Environment.CurrentDirectory, documentRelativePath));
            Console.WriteLine($"Opening file '{uri.LocalPath}.'");
            var text = File.ReadAllText(uri.LocalPath);
            Console.WriteLine($"File contents:\n{text}\n*****************\n");
            var rhetosAppContext =  serviceProvider.GetRequiredService<RhetosAppContext>();
            var documentFactory = serviceProvider.GetRequiredService<RhetosDocumentFactory>();
            var rhetosDocument = documentFactory.CreateNew(uri);
            rhetosDocument.UpdateText(text);

            var rootPathConfiguration = rhetosAppContext.GetRhetosAppRootPath(rhetosDocument);
            Console.WriteLine($"\nRoot Path Configuration:\n{rootPathConfiguration?.ConfigurationType} = {rootPathConfiguration?.RootPath}\n  from {rootPathConfiguration?.Context}");

            Assert.AreEqual(expectedConfigurationType, rootPathConfiguration.ConfigurationType);

            if (expectedRootAppPath == null)
            {
                Assert.IsNull(rootPathConfiguration.RootPath);
            }
            else
            {
                if (rootPathConfiguration.ConfigurationType == RootPathConfigurationType.DetectedRhetosApp)
                    expectedRootAppPath = Path.Combine(Environment.CurrentDirectory, expectedRootAppPath);

                Assert.AreEqual(expectedRootAppPath, rootPathConfiguration.RootPath);
            }
        }
    }
}
