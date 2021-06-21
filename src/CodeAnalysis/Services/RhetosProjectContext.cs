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
        public Dictionary<string, ConceptDocumentation> Documentation => current?.DslDocumentation?.Concepts;

        private readonly ILogger<RhetosProjectContext> log;
        private static readonly object _syncRoot = new();
        private Context current;

        private class Context
        {
            public IDslSyntaxProvider DslSyntaxProvider { get; }
            public DslSyntax DslSyntax { get; }
            public DslDocumentation DslDocumentation { get; }
            public DateTime DslSyntaxLastModifiedTime { get; }
            public Dictionary<string, ConceptType[]> Keywords { get; }
            public DateTime CreatedTime { get; }

            public Context(IDslSyntaxProvider dslSyntaxProvider)
            {
                DslSyntaxProvider = dslSyntaxProvider;
                DslSyntax = DslSyntaxProvider.Load();
                DslDocumentation = DslSyntaxProvider.LoadDocumentation();
                DslSyntaxLastModifiedTime = DslSyntaxProvider.GetLastModifiedTime();
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

                if (ProjectRootPath == dslSyntaxProvider.ProjectRootPath && current.DslSyntaxLastModifiedTime == dslSyntaxProvider.GetLastModifiedTime())
                    throw new InvalidOperationException(
                        $"Trying to initialize with rootPath='{dslSyntaxProvider.ProjectRootPath}', but {nameof(RhetosProjectContext)} is already successfully initialized with same rootPath and same file DslSyntax timestamps.");

                current = new Context(dslSyntaxProvider);

                log.LogDebug($"Initialized with RootPath='{ProjectRootPath}'.");
            }
        }

        public void UpdateDslSyntax()
        {
            lock (_syncRoot)
            {
                if (!IsInitialized)
                    throw new InvalidOperationException($"Trying to update project context which is not initialized.");

                var dslSyntaxProvider = new DslSyntaxProvider(ProjectRootPath);

                // project is no longer valid
                if (!DslSyntaxProvider.IsValidProjectRootPath(ProjectRootPath))
                {
                    current = null;
                    return;
                }

                if (current.DslSyntaxLastModifiedTime != dslSyntaxProvider.GetLastModifiedTime())
                    Initialize(dslSyntaxProvider);
            }
        }

        private static Dictionary<string, ConceptType[]> ExtractKeywords(DslSyntax dslSyntax)
        {
            if (dslSyntax?.ConceptTypes == null)
                throw new InvalidOperationException($"Provided DslSyntax.json is not valid. Property 'ConceptTypes' expected.");

            var keywordDictionary = dslSyntax.ConceptTypes
                .Select(type => (keyword: type.Keyword, type))
                .Where(info => !string.IsNullOrEmpty(info.keyword))
                .GroupBy(info => info.keyword)
                .ToDictionary(group => group.Key, group => group.Select(info => info.type).ToArray(), StringComparer.InvariantCultureIgnoreCase);

            return keywordDictionary;
        }
    }
}
