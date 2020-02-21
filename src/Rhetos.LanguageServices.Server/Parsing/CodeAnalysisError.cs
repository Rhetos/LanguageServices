using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class CodeAnalysisError
    {
        public enum ErrorSeverity
        {
            Error,
            Warning
        }

        public LineChr LineChr { get; set; } = LineChr.Zero;
        public string Message { get; set; }
        public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;

        public override string ToString()
        {
            return $"{LineChr} {Message}";
        }
    }
}
