using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class AnalysisResult
    {
        public int Line { get; }
        public int Chr { get; }
        public List<IConceptInfo> ConceptContext { get; set; } = new List<IConceptInfo>();
        public Token KeywordToken { get; set; }
        public List<AnalysisError> Errors { get; } = new List<AnalysisError>();

        public AnalysisResult(int line, int chr)
        {
            this.Line = line;
            this.Chr = chr;
        }
    }
}
