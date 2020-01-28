using System;
using System.Collections.Generic;
using System.Linq;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.Logging;
using Rhetos.Utilities;
using DslParser = Rhetos.LanguageServices.Server.RhetosTmp.DslParser;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class RheDocument : IDslScriptsProvider
    {
        public Tokenizer Tokenizer { get; }

        public IEnumerable<DslScript> DslScripts => new[] { new DslScript() { Script = textDocument.Text } };

        private static readonly string[] _lineSeparators = new[] {"\r\n", "\n"};
        private readonly TextDocument textDocument;
        private readonly ILogProvider logProvider;
        private readonly RhetosAppContext rhetosAppContext;

        public RheDocument(string text, RhetosAppContext rhetosAppContext, ILogProvider logProvider)
        {
            this.textDocument = new TextDocument(text);
            Tokenizer = new Tokenizer(this, new FilesUtility(logProvider));
            this.logProvider = logProvider;
            this.rhetosAppContext = rhetosAppContext;
        }

        public AnalysisResult GetAnalysis(int line, int chr)
        {
            var analysisRun = new AnalysisRun(textDocument, Tokenizer, rhetosAppContext, logProvider);
            return analysisRun.RunForPosition(line, chr);
        }

        public string ShowPosition(int line, int chr)
        {
            var pos = textDocument.GetPosition(line, chr);
            var lineText = textDocument.ExtractLine(pos);
            return TextDocument.ShowPositionOnLine(lineText, chr);
        }

        public Token GetTokenBeingTypedAtCursor(int line, int chr)
        {
            if (GetTokenAtPosition(line, chr) != null)
                return null;

            return GetTokenLeftOfPosition(line, chr);
        }

        public Token GetTokenAtPosition(int line, int chr)
        {
            var tokens = Tokenizer.GetTokens();
            var position = textDocument.GetPosition(line, chr);

            foreach (var token in tokens)
            {
                if (position >= token.PositionInDslScript && position < token.PositionInDslScript + token.Value.Length)
                    return token;
            }

            return null;
        }

        public Token GetTokenLeftOfPosition(int line, int chr)
        {
            if (chr > 0) chr--;
            return GetTokenAtPosition(line, chr);
        }
    }
}
