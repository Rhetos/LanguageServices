using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;
using Rhetos.Utilities;

namespace Sandbox
{
    public class DslSyntax : IDslSyntax
    {
        public string Version { get; set; }
        public ExcessDotInKey ExcessDotInKey { get; set; }
        public string DatabaseLanguage { get; set; }
        public ConceptType[] ConceptTypes { get; set; }
    }
}
