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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Tools;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.CodeAnalysis.Services
{
    public class RhetosProjectRootPathResolver : IRhetosProjectRootPathResolver
    {
        public RhetosProjectRootPathResolver()
        {
        }

        public RootPathConfiguration ResolveRootPathFromDocumentDirective(RhetosDocument rhetosDocument)
        {
            try
            {
                var fromDirective = GetRootPathFromText(rhetosDocument.TextDocument.Text);
                if (fromDirective != null)
                {
                    var absolutePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(rhetosDocument.DocumentUri.LocalPath), fromDirective));
                    return new RootPathConfiguration(absolutePath, RootPathConfigurationType.SourceDirective, rhetosDocument.DocumentUri.LocalPath);
                }

                return null;
            }
            catch (Exception e)
            {
                return new RootPathConfiguration(null, RootPathConfigurationType.None, e.Message);
            }
        }

        public RootPathConfiguration ResolveRootPathForDocument(RhetosDocument rhetosDocument)
        {
            try
            {
                var fromDirective = ResolveRootPathFromDocumentDirective(rhetosDocument);
                if (fromDirective != null && !string.IsNullOrEmpty(fromDirective.RootPath))
                    return fromDirective;

                var fromConfiguration = GetRootPathFromConfigurationInParentFolders(Path.GetDirectoryName(rhetosDocument.DocumentUri.LocalPath));
                if (fromConfiguration.rootPath != null)
                    return new RootPathConfiguration(fromConfiguration.rootPath, RootPathConfigurationType.ConfigurationFile, fromConfiguration.configurationPath);

                var fromDetected = GetRootPathInParentFolders(Path.GetDirectoryName(rhetosDocument.DocumentUri.LocalPath));
                if (fromDetected != null)
                    return new RootPathConfiguration(fromDetected, RootPathConfigurationType.DetectedRhetosApp, fromDetected);
            }
            catch (Exception e)
            {
                return new RootPathConfiguration(null, RootPathConfigurationType.None, e.Message);
            }

            return new RootPathConfiguration(null, RootPathConfigurationType.None, null);
        }

        private string GetRootPathFromText(string text)
        {
            var pathMatch = Regex.Match(text, @"^\s*//\s*<rhetosProjectRootPath=""(.+)""\s*/>");
            var rootPath = pathMatch.Success
                ? pathMatch.Groups[1].Value
                : null;

            return rootPath;
        }

        private string GetRootPathInParentFolders(string startingFolder)
        {
            var rhetosProjectRootFolder = EnumerateParentFolders(startingFolder)
                .FirstOrDefault(IsValidRhetosProjectFolder);

            return rhetosProjectRootFolder;
        }

        private bool IsValidRhetosProjectFolder(string folder)
        {
            return DslSyntaxProvider.IsValidProjectRootPath(folder);
        }

        private (string rootPath, string configurationPath) GetRootPathFromConfigurationInParentFolders(string startingFolder)
        {
            var configurationFile = EnumerateParentFolders(startingFolder)
                .Select(folder => Path.Combine(folder, AppLanguageServicesOptions.ConfigurationFilename))
                .FirstOrDefault(filename => File.Exists(filename));

            if (configurationFile == null)
                return (null, null);

            var appOptions = JsonConvert.DeserializeObject<AppLanguageServicesOptions>(File.ReadAllText(configurationFile));

            if (string.IsNullOrEmpty(appOptions.RhetosProjectRootPath))
                throw new InvalidOperationException($"Configuration file '{configurationFile}' does not contain valid configuration key {nameof(appOptions.RhetosProjectRootPath)}.");

            string absoluteRootPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(configurationFile),
                appOptions.RhetosProjectRootPath));

            return (absoluteRootPath, Path.GetFullPath(configurationFile));
        }

        private IEnumerable<string> EnumerateParentFolders(string startingFolder)
        {
            if (string.IsNullOrEmpty(startingFolder)) yield break;
            var folder = Path.GetFullPath(startingFolder);
            while (Directory.Exists(folder))
            {
                yield return folder;

                var parent = Path.GetFullPath(Path.Combine(folder, ".."));
                if (parent == folder) yield break;
                folder = parent;
            }
        }

    }
}
