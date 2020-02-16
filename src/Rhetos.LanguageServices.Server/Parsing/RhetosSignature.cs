using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
