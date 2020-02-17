using System;
using System.Collections.Generic;
using Rhetos.Dsl;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class RhetosSignature
    {
        public Type ConceptInfoType { get; set; }
        public List<ConceptMember> Parameters { get; set; }
        public string Signature { get; set; }
        public string Documentation { get; set; }
    }
}
