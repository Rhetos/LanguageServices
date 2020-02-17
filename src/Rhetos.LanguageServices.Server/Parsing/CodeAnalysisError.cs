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
