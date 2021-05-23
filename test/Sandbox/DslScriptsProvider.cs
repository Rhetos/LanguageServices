using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;

namespace Sandbox
{
    public class DslScriptsProvider : IDslScriptsProvider
    {
        public IEnumerable<DslScript> DslScripts { get; }

        public DslScriptsProvider(string script)
        {
            var dslScript = new DslScript()
            {
                Name = "TextScript",
                Path = "/",
                Script = script
            };

            DslScripts = new List<DslScript>() {dslScript};
        }
    }
}
