using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class RhetosDocument : IDslScriptsProvider
    {
        public Tokenizer Tokenizer { get; private set; }
        public TextDocument TextDocument { get; private set; }
        public List<CodeAnalysisError> TokenizerErrors { get; private set; } = new List<CodeAnalysisError>();
        public List<CodeAnalysisError> CodeAnalysisErrors { get; private set; } = new List<CodeAnalysisError>();
        public IEnumerable<CodeAnalysisError> AllAnalysisErrors => TokenizerErrors.Concat(CodeAnalysisErrors);
        public DateTime LastCodeAnalysisRun { get; private set; } = DateTime.MinValue;

        public IEnumerable<DslScript> DslScripts => new[] { new DslScript() { Script = TextDocument.Text } };

        private static readonly string[] _lineSeparators = new[] {"\r\n", "\n"};
        private readonly RhetosAppContext rhetosAppContext;
        private readonly object _syncAnalysis = new object();
        private readonly ILoggerFactory logFactory;
        private readonly ConceptQueries conceptQueries;

        public RhetosDocument(RhetosAppContext rhetosAppContext, ConceptQueries conceptQueries, ILoggerFactory logFactory)
        {
            this.rhetosAppContext = rhetosAppContext;
            this.logFactory = logFactory;
            this.conceptQueries = conceptQueries;
        }

        public void UpdateText(string text)
        {
            lock (_syncAnalysis)
            {
                TextDocument = new TextDocument(text);
                Tokenizer = CreateTokenizerWithCapturedErrors();
                var analysisRun = new CodeAnalysisRun(TextDocument, Tokenizer, rhetosAppContext, logFactory);
                var analysisResult = analysisRun.RunForPosition(0, 0);
                CodeAnalysisErrors = analysisResult.Errors;
                LastCodeAnalysisRun = DateTime.Now;
            }
            logFactory.CreateLogger("UpdateText").LogInformation("COMPLETE");
        }

        public CodeAnalysisResult GetAnalysis(int line, int chr)
        {
            lock (_syncAnalysis)
            {
                var analysisRun = new CodeAnalysisRun(TextDocument, Tokenizer, rhetosAppContext, logFactory);
                return analysisRun.RunForPosition(line, chr);
            }
        }

        public List<string> GetCompletionKeywordsAtPosition(int line, int chr)
        {
            var analysisResult = GetAnalysis(line, chr);

            var typingToken = GetTokenBeingTypedAtCursor(line, chr);
            if (analysisResult.KeywordToken != null && analysisResult.KeywordToken != typingToken)
                return new List<string>();

            var lastParent = analysisResult.ConceptContext.LastOrDefault();
            var validConcepts = lastParent == null
                ? rhetosAppContext.ConceptInfoTypes.ToList()
                : conceptQueries.ValidConceptsForParent(lastParent.GetType());

            var keywords = validConcepts
                .Select(concept => ConceptInfoHelper.GetKeyword(concept))
                .Where(keyword => keyword != null)
                .Distinct()
                .ToList();

            return keywords;
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
            var tokenizer = new Tokenizer(this, new FilesUtility(new RhetosNetCoreLogProvider(logFactory)));
            TokenizerErrors.Clear();
            try
            {
                _ = tokenizer.GetTokens();
            }
            catch (DslParseSyntaxException e)
            {
                var (line, chr) = TextDocument.GetLineChr(e.Position);
                TokenizerErrors.Add(new CodeAnalysisError() { Message = e.SimpleMessage, Line = line, Chr = chr });
            }
            catch (Exception e)
            {
                TokenizerErrors.Add(new CodeAnalysisError() {Message = e.Message});
            }
            return tokenizer;
        }
    }
}
