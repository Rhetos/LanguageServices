using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Parsing
{
    internal class CodeAnalysisRun
    {
        private CodeAnalysisResult result = null;
        private readonly Tokenizer tokenizer;
        private readonly RhetosAppContext rhetosAppContext;
        private int targetPos;
        private readonly TextDocument textDocument;
        private readonly ILogProvider rhetosLogProvider;
        private readonly List<Token> tokens;

        public CodeAnalysisRun(TextDocument textDocument, Tokenizer tokenizer, RhetosAppContext rhetosAppContext, ILoggerFactory logFactory)
        {
            this.tokenizer = tokenizer;
            this.rhetosAppContext = rhetosAppContext;
            this.textDocument = textDocument;
            this.rhetosLogProvider = new RhetosNetCoreLogProvider(logFactory);
            this.tokens = tokenizer.GetTokens();
        }

        public CodeAnalysisResult RunForPosition(int line, int chr)
        {
            if (result != null) throw new InvalidOperationException("Analysis already run.");
            result = new CodeAnalysisResult(line, chr);
            targetPos = textDocument.GetPosition(line, chr);
            var dslParser = new DslParser(tokenizer, rhetosAppContext.ConceptInfoInstances, rhetosLogProvider);
            try
            {
                dslParser.ParseConceptsWithCallbacks(OnKeyword, null, OnUpdateContext);
            }
            catch (DslParseSyntaxException e)
            {
                result.Errors.Add(CreateAnalysisError(e));
            }
            catch (Exception e)
            {
                result.Errors.Add(new CodeAnalysisError() { Line = 0, Chr = 0, Message = e.Message });
            }
            return result;
        }

        private CodeAnalysisError CreateAnalysisError(DslParseSyntaxException e)
        {
            var (line, chr) = textDocument.GetLineChr(e.Position);
            return new CodeAnalysisError() {Message = e.SimpleMessage, Line = line, Chr = chr};
        }

        private void OnUpdateContext(ITokenReader iTokenReader, Stack<IConceptInfo> context, bool isOpening)
        {
            var tokenReader = (TokenReader)iTokenReader;
            var lastToken = tokens[tokenReader.PositionInTokenList - 1];
            var contextPos = lastToken.PositionInDslScript + lastToken.Value.Length;
            if (contextPos <= targetPos)
                result.ConceptContext = context.Reverse().ToList();
        }

        private void OnKeyword(ITokenReader iTokenReader, string keyword)
        {
            var tokenReader = (TokenReader)iTokenReader;
            if (tokenReader.PositionInTokenList >= tokens.Count) return;
            
            var lastToken = tokens[tokenReader.PositionInTokenList];
            if (targetPos >= lastToken.PositionInDslScript)
            {
                if (keyword != null)
                    result.KeywordToken = lastToken;
                else
                    result.KeywordToken = null;
            }
        }
    }
}
