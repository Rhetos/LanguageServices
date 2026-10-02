/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.CodeAnalysis.Tools;
using Rhetos.LanguageServices.CommonTestTools;

namespace Rhetos.LanguageServices.CodeAnalysis.Test
{
    [TestClass]
    [DeploymentItem("RhetosAppFolder\\", "RhetosAppFolder")]
    [DeploymentItem("RhetosAppFolder\\FolderWithApp\\MockObj\\Rhetos\\", "RhetosAppFolder\\FolderWithApp\\obj\\Rhetos")]
    [DeploymentItem("DslSyntax.json", "RhetosAppFolder\\FolderWithApp\\obj\\Rhetos\\")]
    public class ProjectRootPathResolverTests
    {
        private readonly IServiceProvider serviceProvider;

        public ProjectRootPathResolverTests()
        {
            serviceProvider = TestCommon.CreateTestServiceProvider();
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
        public void RhetosRootAppPathFromDocument(string documentRelativePath, string expectedRootAppPath, RootPathConfigurationType expectedConfigurationType)
        {
            var documentKey = TestCommon.DocumentKeyFromPath(Path.Combine(Environment.CurrentDirectory, documentRelativePath));
            Console.WriteLine($"Opening file '{documentKey.LocalPath}.'");
            var text = File.ReadAllText(documentKey.LocalPath);
            Console.WriteLine($"File contents:\n{text}\n*****************\n");
            var rhetosProjectRootPathResolver =  serviceProvider.GetRequiredService<IRhetosProjectRootPathResolver>();
            var documentFactory = serviceProvider.GetRequiredService<RhetosDocumentFactory>();
            var rhetosDocument = documentFactory.CreateNew(documentKey);
            rhetosDocument.UpdateText(text);

            var rootPathConfiguration = rhetosProjectRootPathResolver.ResolveRootPathForDocument(rhetosDocument);
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

        [TestMethod]
        [DataRow("// <rhetosProjectRootPath=\".\\\"/>", "relative rhetosProjectRootPath directive")]
        [DataRow("// <rhetosProjectRootPath=\"RhetosAppFolder\\FolderWithApp\"/>", "relative rhetosProjectRootPath directive")]
        [DataRow("", "cannot be detected from its folder")]
        [DataRow("Module m;", "cannot be detected from its folder")]
        public void NonFileDocumentHasNoRootPath(string text, string expectedReason)
        {
            var rhetosProjectRootPathResolver = serviceProvider.GetRequiredService<IRhetosProjectRootPathResolver>();
            var documentFactory = serviceProvider.GetRequiredService<RhetosDocumentFactory>();
            var rhetosDocument = documentFactory.CreateNew(DocumentKey.ForNonFile("untitled:Untitled-1"));
            rhetosDocument.UpdateText(text);

            foreach (var rootPathConfiguration in new[] { rhetosProjectRootPathResolver.ResolveRootPathForDocument(rhetosDocument), rhetosDocument.RootPathConfiguration })
            {
                Console.WriteLine($"{rootPathConfiguration.ConfigurationType} = {rootPathConfiguration.RootPath}, context: {rootPathConfiguration.Context}");
                // The relative directive must not be resolved against the process working directory (Environment.CurrentDirectory contains a Rhetos app folder).
                Assert.IsNull(rootPathConfiguration.RootPath);
                Assert.AreEqual(RootPathConfigurationType.None, rootPathConfiguration.ConfigurationType);
                StringAssert.Contains(rootPathConfiguration.Context, "'untitled:Untitled-1' is not a file", StringComparison.Ordinal);
                StringAssert.Contains(rootPathConfiguration.Context, expectedReason, StringComparison.Ordinal);
            }

            var errors = rhetosDocument.GetAnalysis().AllErrors.ToList();
            Assert.AreEqual(1, errors.Count, string.Join(Environment.NewLine, errors));
            Assert.AreEqual(CodeAnalysisError.ErrorSeverity.Warning, errors[0].Severity);
            StringAssert.Contains(errors[0].Message, "No valid RhetosProjectRootPath configuration was found for this document (the document 'untitled:Untitled-1' is not a file", StringComparison.Ordinal);
        }

        [TestMethod]
        public void NonFileDocumentWithAbsoluteDirective()
        {
            var rhetosProjectRootPathResolver = serviceProvider.GetRequiredService<IRhetosProjectRootPathResolver>();
            var documentFactory = serviceProvider.GetRequiredService<RhetosDocumentFactory>();
            var rhetosDocument = documentFactory.CreateNew(DocumentKey.ForNonFile("untitled:Untitled-1"));
            var appFolder = Path.Combine(Environment.CurrentDirectory, @"RhetosAppFolder\FolderWithApp");
            rhetosDocument.UpdateText($"// <rhetosProjectRootPath=\"{appFolder}\"/>");

            var rootPathConfiguration = rhetosProjectRootPathResolver.ResolveRootPathForDocument(rhetosDocument);

            Assert.AreEqual(RootPathConfigurationType.SourceDirective, rootPathConfiguration.ConfigurationType);
            Assert.AreEqual(appFolder, rootPathConfiguration.RootPath);
            Assert.AreEqual("untitled:Untitled-1", rootPathConfiguration.Context);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void DirectiveAddedAndRemoved(bool isFile)
        {
            var documentFactory = serviceProvider.GetRequiredService<RhetosDocumentFactory>();
            var documentKey = isFile
                ? TestCommon.DocumentKeyFromPath(Path.Combine(Environment.CurrentDirectory, @"RhetosAppFolder\EmptyFolder\added-and-removed.rhe"))
                : DocumentKey.ForNonFile("untitled:Untitled-1");
            var rhetosDocument = documentFactory.CreateNew(documentKey);
            var appFolder = Path.Combine(Environment.CurrentDirectory, @"RhetosAppFolder\FolderWithApp");

            rhetosDocument.UpdateText($"// <rhetosProjectRootPath=\"{appFolder}\"/>\nModule M;");
            Assert.AreEqual(RootPathConfigurationType.SourceDirective, rhetosDocument.RootPathConfiguration.ConfigurationType);
            Assert.AreEqual(appFolder, rhetosDocument.RootPathConfiguration.RootPath);

            // Removing the directive must re-resolve the root path (there is no Rhetos app or configuration file above EmptyFolder).
            rhetosDocument.UpdateText("Module M;");
            Assert.AreEqual(RootPathConfigurationType.None, rhetosDocument.RootPathConfiguration.ConfigurationType);
            Assert.IsNull(rhetosDocument.RootPathConfiguration.RootPath);
            Assert.IsFalse(rhetosDocument.UpdateRootPathIfChanged());
        }

        [TestMethod]
        public void FileDocumentWithRelativeDirective()
        {
            var rhetosProjectRootPathResolver = serviceProvider.GetRequiredService<IRhetosProjectRootPathResolver>();
            var documentFactory = serviceProvider.GetRequiredService<RhetosDocumentFactory>();
            var documentPath = Path.Combine(Environment.CurrentDirectory, @"RhetosAppFolder\EmptyFolder\relative-directive.rhe");
            var rhetosDocument = documentFactory.CreateNew(TestCommon.DocumentKeyFromPath(documentPath));
            rhetosDocument.UpdateText("// <rhetosProjectRootPath=\"..\\FolderWithApp\"/>");

            var rootPathConfiguration = rhetosProjectRootPathResolver.ResolveRootPathForDocument(rhetosDocument);

            Assert.AreEqual(RootPathConfigurationType.SourceDirective, rootPathConfiguration.ConfigurationType);
            Assert.AreEqual(Path.Combine(Environment.CurrentDirectory, @"RhetosAppFolder\FolderWithApp"), rootPathConfiguration.RootPath);
            Assert.AreEqual(documentPath, rootPathConfiguration.Context);
        }
    }
}
