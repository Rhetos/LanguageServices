using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using Rhetos.Deployment;
using Rhetos.Dsl;
using Rhetos.Extensibility;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.Server.Services
{
    public class RhetosAppContext
    {
        public bool IsInitialized { get; private set; }
        public string RootPath { get; private set; }
        public Dictionary<string, Type[]> Keywords { get; private set; } = new Dictionary<string, Type[]>();
        public Type[] ConceptInfoTypes { get; private set; }
        public IConceptInfo[] ConceptInfoInstances { get; private set; }

        private readonly ILogger<RhetosAppContext> log;
        private readonly ILogProvider rhetosLogProvider;

        public RhetosAppContext(ILoggerFactory logFactory)
        {
            this.log = logFactory.CreateLogger<RhetosAppContext>();
            this.rhetosLogProvider = new RhetosNetCoreLogProvider(logFactory);
        }

        public void InitializeFromCurrentDomain()
        {
            var conceptInfoTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(a => typeof(IConceptInfo).IsAssignableFrom(a) && a.IsClass)
                .ToArray();

            InitializeFromConceptTypes(conceptInfoTypes);
            RootPath = null;
        }

        public void InitializeFromAppPath(string rootPath)
        {
            var sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(rootPath))
                throw new ArgumentException($"Error initializing rhetos app, specified rootPath '{rootPath}' is not valid!", rootPath);

            var configurationProvider = new ConfigurationBuilder()
                .AddRhetosAppConfiguration(rootPath)
                .Build();

            var listAssemblies = LegacyUtilities.GetListAssembliesDelegate(configurationProvider);

            var resolveDelegate = CreateAssemblyResolveDelegate(listAssemblies().ToList());

            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += resolveDelegate;

                var builder = new RhetosContainerBuilder(configurationProvider, rhetosLogProvider, listAssemblies);
                var scanner = builder.GetPluginScanner();
                var conceptInfoTypes = scanner.FindPlugins(typeof(IConceptInfo)).Select(a => a.Type).ToArray();
                InitializeFromConceptTypes(conceptInfoTypes);

                log.LogInformation($"Found IConceptInfo: {conceptInfoTypes.Length} in {sw.ElapsedMilliseconds} ms.");
                RootPath = rootPath;
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolveDelegate;
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
        }

        private void LoadKeywords()
        {
            Keywords = ConceptInfoTypes
                .Select(info => (keywordAttribute: info.GetCustomAttribute(typeof(ConceptKeywordAttribute)) as ConceptKeywordAttribute, infoType: info))
                .Select(info => (keyword: info.keywordAttribute?.Keyword, info.infoType))
                .Where(info => info.keyword != null)
                .GroupBy(info => info.keyword)
                .ToDictionary(group => group.Key, group => group.Select(info => info.infoType).ToArray());
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
