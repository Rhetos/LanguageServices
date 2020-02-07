using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.Logging;
using DslParser = Rhetos.LanguageServices.Server.RhetosTmp.DslParser;

namespace Rhetos.LanguageServices.Server.Parsing
{
    internal class CodeAnalysisRun
    {
        private CodeAnalysisResult result = null;
        private readonly Tokenizer tokenizer;
        private readonly RhetosAppContext rhetosAppContext;
        private int targetPos;
        private readonly TextDocument textDocument;
        private readonly ILogProvider logProvider;
        private readonly List<Token> tokens;

        public CodeAnalysisRun(TextDocument textDocument, Tokenizer tokenizer, RhetosAppContext rhetosAppContext, ILogProvider logProvider)
        {
            this.tokenizer = tokenizer;
            this.rhetosAppContext = rhetosAppContext;
            this.textDocument = textDocument;
            this.logProvider = logProvider;
            this.tokens = tokenizer.GetTokens();
        }

        public CodeAnalysisResult RunForPosition(int line, int chr)
        {
            if (result != null) throw new InvalidOperationException("Analysis already run.");
            result = new CodeAnalysisResult(line, chr);
            targetPos = textDocument.GetPosition(line, chr);
            var dslParser = new DslParser(tokenizer, rhetosAppContext.ConceptInfoInstances, logProvider);
            try
            {
                dslParser.ParseConceptsWithCallbacks(OnKeyword, null, OnUpdateContext);
            }
            catch (DslSyntaxException e)
            {
                result.Errors.Add(CreateAnalysisError(e));
            }
            catch (Exception e)
            {
                result.Errors.Add(new CodeAnalysisError() { Line = 0, Chr = 0, Message = e.Message });
            }
            return result;
        }

        private CodeAnalysisError CreateAnalysisError(DslSyntaxException e)
        {
            var positionMatch = Regex.Match(e.Message, @"At line ([0-9]+), column ([0-9]+)");
            var error = new CodeAnalysisError()
            {
                Line = int.Parse(positionMatch.Groups[1].Value) - 1,
                Chr = int.Parse(positionMatch.Groups[2].Value) - 1,
                Message = e.Message
            };
            return error;
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
