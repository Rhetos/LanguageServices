using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;

namespace Sandbox.Parsing
{
    public class RhetosProjectContext
    {
        public IDslSyntax DslSyntax { get; }
        public Dictionary<string, ConceptType[]> Keywords => keywords.Value;

        private readonly Lazy<Dictionary<string, ConceptType[]>> keywords;

        public RhetosProjectContext(IDslSyntax dslSyntax)
        {
            DslSyntax = dslSyntax;
            keywords = new Lazy<Dictionary<string, ConceptType[]>>(ExtractKeywords);
        }

        public Dictionary<string, ConceptType[]> ExtractKeywords()
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
