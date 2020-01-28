using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class AnalysisError
    {
        public int Line { get; set; }
        public int Chr { get; set; }
        public string Message { get; set; }
    }
}
