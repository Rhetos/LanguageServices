using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.Logging;
using Rhetos.Utilities;
using DslParser = Rhetos.LanguageServices.Server.RhetosTmp.DslParser;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class RhetosDocument : IDslScriptsProvider
    {
        public Tokenizer Tokenizer { get; private set; }
        public TextDocument TextDocument { get; private set; }
        public List<CodeAnalysisError> TokenizerErrors { get; private set; } = new List<CodeAnalysisError>();
        public List<CodeAnalysisError> AnalysisErrors { get; private set; } = new List<CodeAnalysisError>();

        public IEnumerable<DslScript> DslScripts => new[] { new DslScript() { Script = TextDocument.Text } };

        private static readonly string[] _lineSeparators = new[] {"\r\n", "\n"};
        private readonly ILogProvider logProvider;
        private readonly RhetosAppContext rhetosAppContext;
        private readonly object _syncAnalysis = new object();

        public RhetosDocument(RhetosAppContext rhetosAppContext, ILogProvider logProvider)
        {
            this.logProvider = logProvider;
            this.rhetosAppContext = rhetosAppContext;
        }

        public void UpdateText(string text)
        {
            lock (_syncAnalysis)
            {
                TextDocument = new TextDocument(text);
                Tokenizer = CreateTokenizerWithCapturedErrors();
                var analysisRun = new CodeAnalysisRun(TextDocument, Tokenizer, rhetosAppContext, logProvider);
                var analysisResult = analysisRun.RunForPosition(0, 0);
                AnalysisErrors = analysisResult.Errors;
            }
            logProvider.GetLogger("UpdateText").Info("COMPLETE");
        }

        public CodeAnalysisResult GetAnalysis(int line, int chr)
        {
            lock (_syncAnalysis)
            {
                var analysisRun = new CodeAnalysisRun(TextDocument, Tokenizer, rhetosAppContext, logProvider);
                return analysisRun.RunForPosition(line, chr);
            }
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
            var position = TextDocument.GetPosition(line, chr);

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

        // Due to unusual way the tokenizer works, if we capture errors during initial call to GetToken(),
        // valid tokens will be returned without error in subsequent calls
        private Tokenizer CreateTokenizerWithCapturedErrors()
        {
            var tokenizer = new Tokenizer(this, new FilesUtility(logProvider));
            TokenizerErrors.Clear();
            try
            {
                _ = tokenizer.GetTokens();
            }
            catch (DslSyntaxException e)
            {
                TokenizerErrors.Add(new CodeAnalysisError() { Message = e.Message });
            }
            return tokenizer;
        }
    }
}
