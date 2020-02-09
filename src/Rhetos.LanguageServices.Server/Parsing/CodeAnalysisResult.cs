using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class CodeAnalysisResult
    {
        public int Line { get; }
        public int Chr { get; }
        public List<IConceptInfo> ConceptContext { get; set; } = new List<IConceptInfo>();
        public Token KeywordToken { get; set; }
        public Token NextKeywordToken { get; set; }
        public List<CodeAnalysisError> Errors { get; } = new List<CodeAnalysisError>();

        public Dictionary<string, List<string>> MemberDebug = new Dictionary<string, List<string>>();

        public CodeAnalysisResult(int line, int chr)
        {
            this.Line = line;
            this.Chr = chr;
        }
    }
}
