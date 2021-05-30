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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.CodeAnalysis.Tools;

namespace Rhetos.LanguageServices.CodeAnalysis.Test
{
    [TestClass]
    [DeploymentItem("RhetosAppFolder\\", "RhetosAppFolder")]
    [DeploymentItem("RhetosAppFolder\\FolderWithApp\\MockObj\\Rhetos\\", "RhetosAppFolder\\FolderWithApp\\obj\\Rhetos")]
    public class RhetosProjectContextTests
    {
        private readonly IServiceProvider serviceProvider;

        public RhetosProjectContextTests()
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
        public void RhetoRootAppPathFromDocument(string documentRelativePath, string expectedRootAppPath, RootPathConfigurationType expectedConfigurationType)
        {
            var uri = new Uri(Path.Combine(Environment.CurrentDirectory, documentRelativePath));
            Console.WriteLine($"Opening file '{uri.LocalPath}.'");
            var text = File.ReadAllText(uri.LocalPath);
            Console.WriteLine($"File contents:\n{text}\n*****************\n");
            var rhetosProjectRootPathResolver =  serviceProvider.GetRequiredService<RhetosProjectRootPathResolver>();
            var documentFactory = serviceProvider.GetRequiredService<RhetosDocumentFactory>();
            var rhetosDocument = documentFactory.CreateNew(uri);
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
    }
}
