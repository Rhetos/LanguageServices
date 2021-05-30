using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rhetos.Dsl;
using Rhetos.LanguageServices.CodeAnalysis.Tools;

namespace Rhetos.LanguageServices.CodeAnalysis.Parsing
{
    public class RhetosProjectContext
    {
        // TODO: make dynamic
        public bool IsInitialized { get; private set; }

        // TODO: make dynamic
        public string RootPath { get; private set; }
        public DateTime LastContextUpdateTime { get; private set; }

        public DslSyntax DslSyntax { get; private set; }
        public Dictionary<string, ConceptType[]> Keywords => keywords.Value;

        private Lazy<Dictionary<string, ConceptType[]>> keywords;

        public RhetosProjectContext()
        {
        }

        public void InitializeFromDslSyntax(DslSyntax dslSyntax)
        {
            DslSyntax = dslSyntax;
            keywords = new Lazy<Dictionary<string, ConceptType[]>>(ExtractKeywords);
            IsInitialized = true;
            LastContextUpdateTime = DateTime.Now;
        }

        public void InitializeFromPath(string path)
        {
            var dslSyntaxFile = Path.Combine(path, "DslSyntax.json");
            var dslSyntaxSerialized = File.ReadAllText(dslSyntaxFile);

            var serializerSettings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
            };

            var dslSyntax = JsonConvert.DeserializeObject<DslSyntax>(dslSyntaxSerialized, serializerSettings);
            InitializeFromDslSyntax(dslSyntax);
            RootPath = path;
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

        public RootPathConfiguration GetRhetosProjectRootPath(RhetosDocument rhetosDocument)
        {
            throw new NotImplementedException();
        }
    }
}
