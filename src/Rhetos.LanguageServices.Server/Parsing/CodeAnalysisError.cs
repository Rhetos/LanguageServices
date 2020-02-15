using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class CodeAnalysisError
    {
        public LineChr LineChr;
        public string Message { get; set; }

        public override string ToString()
        {
            return $"{LineChr} {Message}";
        }
    }
}
