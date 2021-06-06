using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Tools;

namespace Rhetos.LanguageServices.CodeAnalysis.Services
{
    public class RhetosProjectRootPathResolver : IRhetosProjectRootPathResolver
    {
        private static readonly string _configurationFilename = "rhetos-language-services.settings.json";
        private static readonly string _rhetosProjectRootPathConfigurationKey = "RhetosProjectRootPath";

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
                .Select(folder => Path.Combine(folder, _configurationFilename))
                .FirstOrDefault(filename => File.Exists(filename));

            if (configurationFile == null)
                return (null, null);

            var configurationProvider = new ConfigurationBuilder()
                .AddJsonFile(configurationFile)
                .Build();

            var rhetosProjectRootPath = configurationProvider.GetValue<string>(_rhetosProjectRootPathConfigurationKey);
            if (string.IsNullOrEmpty(rhetosProjectRootPath))
                throw new InvalidOperationException($"Configuration file '{configurationFile}' does not contain valid configuration key {_rhetosProjectRootPathConfigurationKey}.");

            return (Path.GetFullPath(rhetosProjectRootPath), Path.GetFullPath(configurationFile));
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
