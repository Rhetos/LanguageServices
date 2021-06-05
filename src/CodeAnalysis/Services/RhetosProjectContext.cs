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
        public bool IsInitialized => ProjectRootPath != null;
        public string ProjectRootPath => currentDslSyntaxProvider?.ProjectRootPath;

        public DateTime LastContextUpdateTime { get; private set; }
        public DslSyntax DslSyntax { get; private set; }
        public Dictionary<string, ConceptType[]> Keywords { get; private set; }
        private IDslSyntaxProvider currentDslSyntaxProvider;

        private readonly ILogger<RhetosProjectContext> log;

        public RhetosProjectContext(ILogger<RhetosProjectContext> log)
        {
            this.log = log;
        }

        public void Initialize(IDslSyntaxProvider dslSyntaxProvider)
        {
            if (dslSyntaxProvider == null)
                throw new ArgumentNullException(nameof(dslSyntaxProvider));

            if (string.IsNullOrEmpty(dslSyntaxProvider.ProjectRootPath))
                throw new ArgumentNullException(nameof(dslSyntaxProvider.ProjectRootPath));

            if (ProjectRootPath == dslSyntaxProvider.ProjectRootPath)
                throw new InvalidOperationException(
                    $"Trying to initialize with rootPath='{dslSyntaxProvider.ProjectRootPath}', but {nameof(RhetosProjectContext)} is already successfully initialized with same rootPath.");

            DslSyntax = dslSyntaxProvider.Load();
            Keywords = ExtractKeywords();
            LastContextUpdateTime = DateTime.Now;
            currentDslSyntaxProvider = dslSyntaxProvider;

            log.LogDebug($"Initialized with RootPath='{ProjectRootPath}'.");
        }

        public void UpdateDslSyntax()
        {
            // change to IDslSyntaxProvider pattern?
            throw new NotImplementedException();
        }

        private Dictionary<string, ConceptType[]> ExtractKeywords()
        {
            var keywordDictionary = DslSyntax.ConceptTypes
                .Select(type => (keyword: type.Keyword, type))
                .Where(info => !string.IsNullOrEmpty(info.keyword))
                .GroupBy(info => info.keyword)
                .ToDictionary(group => group.Key, group => group.Select(info => info.type).ToArray(), StringComparer.InvariantCultureIgnoreCase);

            return keywordDictionary;
        }
    }
}
