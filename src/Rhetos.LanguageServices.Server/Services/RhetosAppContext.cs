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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Autofac;
using Microsoft.Extensions.Logging;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;
using Rhetos.Utilities.ApplicationConfiguration;

namespace Rhetos.LanguageServices.Server.Services
{
    public class RhetosAppContext
    {
        private static readonly string _configurationFilename = "rhetos-language-services.settings.json";
        private static readonly string _rhetosAppRootPathConfigurationKey = "RhetosAppRootPath";

        public bool IsInitialized { get; private set; }
        public bool IsInitializedFromCurrentDomain { get; private set; }
        public string RootPath { get; private set; }
        public Dictionary<string, Type[]> Keywords { get; private set; } = new Dictionary<string, Type[]>(StringComparer.InvariantCultureIgnoreCase);
        public Type[] ConceptInfoTypes { get; private set; } = new Type[0];
        public IConceptInfo[] ConceptInfoInstances { get; private set; } = new IConceptInfo[0];
        public CodeAnalysisError LastInitializeError { get; private set; }
        public DateTime LastContextUpdateTime { get; private set; }

        private readonly ILogger<RhetosAppContext> log;
        private readonly ILogProvider rhetosLogProvider;

        public RhetosAppContext(ILoggerFactory logFactory)
        {
            this.log = logFactory.CreateLogger<RhetosAppContext>();
            this.rhetosLogProvider = new RhetosNetCoreLogProvider(logFactory);
        }

        public void InitializeFromCurrentDomain()
        {
            if (IsInitialized)
                throw new InvalidOperationException($"{nameof(RhetosAppContext)} has already been initialized.");

            var conceptInfoTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(a => typeof(IConceptInfo).IsAssignableFrom(a) && a.IsClass)
                .ToArray();

            InitializeFromConceptTypes(conceptInfoTypes);
            IsInitializedFromCurrentDomain = true;
            RootPath = null;
        }

        public void InitializeFromAppPath(string rootPath)
        {
            if (IsInitialized)
                throw new InvalidOperationException($"{nameof(RhetosAppContext)} has already been initialized.");

            if (string.IsNullOrEmpty(rootPath))
                throw new ArgumentException($"Error initializing rhetos app, specified rootPath '{rootPath}' is not valid!", rootPath);

            try
            {
                var sw = Stopwatch.StartNew();
                InitializeFromAppPathInternal(rootPath);
                log.LogInformation($"Initialized Rhetos App Context at '{rootPath}' in {sw.ElapsedMilliseconds} ms. IConceptInfo count: {ConceptInfoTypes.Length}.");
                LastInitializeError = null;
            }
            catch (Exception e)
            {
                log.LogDebug($"Exception while trying to initialize Rhetos app at '{rootPath}': {e}");
                LastInitializeError = new CodeAnalysisError()
                {
                    Message =
                        $"Failed to initialize Language Services from Rhetos app at '{rootPath}'. Either the path is not a valid Rhetos app path or Rhetos app has not been built yet. (Error: {e.Message})",
                    Severity = CodeAnalysisError.ErrorSeverity.Warning
                };
            }

            log.LogTrace($"{nameof(InitializeFromAppPath)} complete.");
        }

        private void InitializeFromAppPathInternal(string rootPath)
        {
            log.LogDebug($"Starting Rhetos app initialization at '{rootPath}'.");
            rootPath = Path.GetFullPath(rootPath);
            var configurationProvider = new ConfigurationBuilder()
                .AddRhetosAppConfiguration(rootPath)
                .Build();

            var listAssemblies = LegacyUtilities.GetListAssembliesDelegate(configurationProvider);
            var assemblyList = listAssemblies().ToList();
            log.LogDebug($"Rhetos configuration reported {assemblyList.Count} assemblies to check for plugins.");

            var resolveDelegate = CreateAssemblyResolveDelegate(assemblyList);

            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += resolveDelegate;

                var builder = new RhetosContainerBuilder(configurationProvider, rhetosLogProvider, listAssemblies);
                var scanner = builder.GetPluginScanner();
                var conceptInfoTypes = scanner.FindPlugins(typeof(IConceptInfo)).Select(a => a.Type).ToArray();
                log.LogDebug($"Plugin scanner found {conceptInfoTypes.Length} IConceptInfo types.");
                InitializeFromConceptTypes(conceptInfoTypes);
                RootPath = rootPath;
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolveDelegate;
            }
        }

        public RootPathConfiguration GetRhetosAppRootPath(RhetosDocument rhetosDocument, bool directiveOnly = false)
        {
            try
            {
                var fromDirective = GetRootPathFromText(rhetosDocument.TextDocument.Text);
                if (fromDirective != null)
                    return new RootPathConfiguration(fromDirective, RootPathConfigurationType.SourceDirective, rhetosDocument.DocumentUri.LocalPath);

                if (!directiveOnly)
                {
                    var fromConfiguration = GetRootPathFromConfigurationInParentFolders(Path.GetDirectoryName(rhetosDocument.DocumentUri.LocalPath));
                    if (fromConfiguration.rootPath != null)
                        return new RootPathConfiguration(fromConfiguration.rootPath, RootPathConfigurationType.ConfigurationFile, fromConfiguration.configurationPath);

                    var fromDetected = GetRootPathInParentFolders(Path.GetDirectoryName(rhetosDocument.DocumentUri.LocalPath));
                    if (fromDetected != null)
                        return new RootPathConfiguration(fromDetected, RootPathConfigurationType.DetectedRhetosApp, fromDetected);
                }
            }
            catch (Exception e)
            {
                return new RootPathConfiguration(null, RootPathConfigurationType.None, e.Message);
            }

            return new RootPathConfiguration(null, RootPathConfigurationType.None, null);
        }

        private string GetRootPathFromText(string text)
        {
            var pathMatch = Regex.Match(text, @"^\s*//\s*<rhetosAppRootPath=""(.+)""\s*/>");
            var rootPath = pathMatch.Success
                ? Path.GetFullPath(pathMatch.Groups[1].Value)
                : null;

            return rootPath;
        }

        private string GetRootPathInParentFolders(string startingFolder)
        {
            var rhetosAppRootFolder = EnumerateParentFolders(startingFolder)
                .FirstOrDefault(RhetosAppEnvironmentProvider.IsRhetosApplicationRootFolder);

            return rhetosAppRootFolder;
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

            var rhetosAppRootPath = configurationProvider.GetValue<string>(_rhetosAppRootPathConfigurationKey);
            if (string.IsNullOrEmpty(rhetosAppRootPath))
                throw new InvalidOperationException($"Configuration file '{configurationFile}' does not contain valid configuration key {_rhetosAppRootPathConfigurationKey}.");

            return (Path.GetFullPath(rhetosAppRootPath), Path.GetFullPath(configurationFile));
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

        private void InitializeFromConceptTypes(Type[] conceptInfoTypes)
        {
            this.ConceptInfoTypes = conceptInfoTypes;

            this.ConceptInfoInstances = ConceptInfoTypes
                .Select(Activator.CreateInstance)
                .Cast<IConceptInfo>()
                .ToArray();

            LoadKeywords();
            IsInitialized = true;
            LastContextUpdateTime = DateTime.Now;
        }

        private void LoadKeywords()
        {
            Keywords = ConceptInfoTypes
                .Select(type => (keyword: ConceptInfoHelper.GetKeyword(type), type))
                .Where(info => !string.IsNullOrEmpty(info.keyword))
                .GroupBy(info => info.keyword)
                .ToDictionary(group => group.Key, group => group.Select(info => info.type).ToArray(), StringComparer.InvariantCultureIgnoreCase);
        }

        // These methods for additional assembly resolving are copied from Rhetos plugin scanner to replicate <probingPath> behavior of Rhetos Apps.
        // They should be refactored and removed from Rhetos and replaced by a better/uniform dll discovery mechanism and then used here
        private ResolveEventHandler CreateAssemblyResolveDelegate(List<string> assemblies)
        {
            var byFilename = assemblies
                .GroupBy(Path.GetFileName)
                .Select(group => new { filename = group.Key, paths = group.OrderBy(path => path.Length).ThenBy(path => path).ToList() })
                .ToList();

            var namesToPaths = byFilename.ToDictionary(dll => dll.filename, dll => dll.paths.First(), StringComparer.InvariantCultureIgnoreCase);
            ResolveEventHandler resolver = (sender, args) => LoadAssemblyFromSpecifiedPaths(sender, args, namesToPaths);

            return resolver;
        }

        private Assembly LoadAssemblyFromSpecifiedPaths(object sender, ResolveEventArgs args, Dictionary<string, string> namesToPaths)
        {
            var filename = $"{new AssemblyName(args.Name).Name}.dll";
            if (namesToPaths.TryGetValue(filename, out var path))
            {
                return Assembly.LoadFrom(path);
            }

            return null;
        }
    }
}
