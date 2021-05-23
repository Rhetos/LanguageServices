using System;
using Rhetos.Dsl;
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.CodeAnalysis.Parsing
{
    public class DslSyntax : IDslSyntax
    {
        public string Version { get; set; }
        public ExcessDotInKey ExcessDotInKey { get; set; }
        public string DatabaseLanguage { get; set; }
        public ConceptType[] ConceptTypes { get; set; }
    }
}
