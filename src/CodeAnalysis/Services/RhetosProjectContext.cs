using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Rhetos.Dsl;
using Rhetos.LanguageServices.CodeAnalysis.Tools;

namespace Rhetos.LanguageServices.CodeAnalysis.Services
{
    public class RhetosProjectContext
    {
        public string ProjectRootPath => current?.DslSyntaxProvider.ProjectRootPath;
        public bool IsInitialized => current != null;
        public DateTime LastContextUpdateTime => current?.CreatedTime ?? DateTime.MinValue;
        public DslSyntax DslSyntax => current?.DslSyntax;
        public Dictionary<string, ConceptType[]> Keywords => current?.Keywords;

        private readonly ILogger<RhetosProjectContext> log;
        private static readonly object _syncRoot = new();
        private Context current;

        private class Context
        {
            public IDslSyntaxProvider DslSyntaxProvider { get; }
            public DslSyntax DslSyntax { get; }
            public Dictionary<string, ConceptType[]> Keywords { get; }
            public DateTime CreatedTime { get; }

            public Context(IDslSyntaxProvider dslSyntaxProvider)
            {
                DslSyntaxProvider = dslSyntaxProvider;
                DslSyntax = DslSyntaxProvider.Load();
                Keywords = ExtractKeywords(DslSyntax);
                CreatedTime = DateTime.Now;
            }
        }

        public RhetosProjectContext(ILogger<RhetosProjectContext> log)
        {
            this.log = log;
        }

        public void Initialize(IDslSyntaxProvider dslSyntaxProvider)
        {
            lock (_syncRoot)
            {
                if (dslSyntaxProvider == null)
                    throw new ArgumentNullException(nameof(dslSyntaxProvider));

                if (string.IsNullOrEmpty(dslSyntaxProvider.ProjectRootPath))
                    throw new ArgumentNullException(nameof(dslSyntaxProvider.ProjectRootPath));

                if (ProjectRootPath == dslSyntaxProvider.ProjectRootPath)
                    throw new InvalidOperationException(
                        $"Trying to initialize with rootPath='{dslSyntaxProvider.ProjectRootPath}', but {nameof(RhetosProjectContext)} is already successfully initialized with same rootPath.");

                current = new Context(dslSyntaxProvider);

                log.LogDebug($"Initialized with RootPath='{ProjectRootPath}'.");
            }
        }

        public void UpdateDslSyntax()
        {
            // TODO: implement reload if lastModified changed
            // throw new NotImplementedException();
        }

        private static Dictionary<string, ConceptType[]> ExtractKeywords(DslSyntax dslSyntax)
        {
            var keywordDictionary = dslSyntax.ConceptTypes
                .Select(type => (keyword: type.Keyword, type))
                .Where(info => !string.IsNullOrEmpty(info.keyword))
                .GroupBy(info => info.keyword)
                .ToDictionary(group => group.Key, group => group.Select(info => info.type).ToArray(), StringComparer.InvariantCultureIgnoreCase);

            return keywordDictionary;
        }
    }
}
