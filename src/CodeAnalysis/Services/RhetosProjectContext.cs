using System;
using System.Collections.Generic;
using System.Linq;
using Rhetos.Dsl;
using Rhetos.LanguageServices.CodeAnalysis.Tools;

namespace Rhetos.LanguageServices.CodeAnalysis.Services
{
    public class RhetosProjectContext
    {
        // TODO: make dynamic
        public bool IsInitialized { get; private set; }

        // TODO: make dynamic
        public string ProjectRootPath { get; private set; }
        public DateTime LastContextUpdateTime { get; private set; }

        public DslSyntax DslSyntax { get; private set; }
        public Dictionary<string, ConceptType[]> Keywords => keywords.Value;

        private Lazy<Dictionary<string, ConceptType[]>> keywords;

        public RhetosProjectContext()
        {
        }

        public void Initialize(DslSyntax dslSyntax, string projectRootPath)
        {
            DslSyntax = dslSyntax;
            keywords = new Lazy<Dictionary<string, ConceptType[]>>(ExtractKeywords);
            IsInitialized = true;
            LastContextUpdateTime = DateTime.Now;
            ProjectRootPath = projectRootPath;
        }

        public void Initialize(string projectRootPath)
        {
            var dslSyntaxProvider = new DslSyntaxProvider(projectRootPath);
            Initialize(dslSyntaxProvider.Load(), projectRootPath);
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
