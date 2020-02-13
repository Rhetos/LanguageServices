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
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.Server.Parsing
{
    internal class CodeAnalysisRun
    {
        private CodeAnalysisResult result = null;
        private readonly RhetosAppContext rhetosAppContext;
        private int targetPos;
        private readonly TextDocument textDocument;
        private readonly ILogProvider rhetosLogProvider;

        public CodeAnalysisRun(TextDocument textDocument, RhetosAppContext rhetosAppContext, ILoggerFactory logFactory)
        {
            this.rhetosAppContext = rhetosAppContext;
            this.textDocument = textDocument;
            this.rhetosLogProvider = new RhetosNetCoreLogProvider(logFactory);
        }

        public CodeAnalysisResult RunForDocument()
        {
            return RunForPosition(LineChr.Zero);
        }

        public CodeAnalysisResult RunForPosition(LineChr lineChr)
        {
            if (result != null) throw new InvalidOperationException("Analysis already run.");
            result = new CodeAnalysisResult(textDocument, lineChr.Line, lineChr.Chr);
            var (tokenizer, capturedErrors) = CreateTokenizerWithCapturedErrors();
            result.Tokens = tokenizer.GetTokens();
            result.TokenizerErrors.AddRange(capturedErrors);
            result.CommentTokens = ParseCommentTokens();

            targetPos = textDocument.GetPosition(lineChr);
            var dslParser = new DslParser(tokenizer, rhetosAppContext.ConceptInfoInstances, rhetosLogProvider);
            try
            {
                dslParser.ParseConceptsWithCallbacks(OnKeyword, OnMemberRead, OnUpdateContext);
            }
            catch (DslParseSyntaxException e)
            {
                result.DslParserErrors.Add(CreateAnalysisError(e));
            }
            catch (Exception e)
            {
                result.DslParserErrors.Add(new CodeAnalysisError() { LineChr = LineChr.Zero, Message = e.Message });
            }
            return result;
        }

        private void OnMemberRead(ITokenReader iTokenReader, IConceptInfo conceptInfo, ConceptMember conceptMember, ValueOrError<object> valueOrError)
        {
            // TODO: monkey patching
            if (!valueOrError.IsError)
            {
                conceptMember.SetMemberValue(conceptInfo, valueOrError.Value);
            }

            var tokenReader = (TokenReader)iTokenReader;
            // if (tokenReader.PositionInTokenList >= tokens.Count) return;
            if (tokenReader.PositionInTokenList == 0) return;

            var lastToken = result.Tokens[tokenReader.PositionInTokenList - 1];
            if (lastToken.PositionInDslScript > targetPos) return;

            if (!result.ValidConcepts.Contains(conceptInfo)) result.ValidConcepts.Add(conceptInfo);

            var type = conceptInfo.GetType().Name;
            if (!result.MemberDebug.ContainsKey(type)) result.MemberDebug[type] = new List<string>();

            var value = valueOrError.IsError ? valueOrError.Error : valueOrError.Value.ToString();

            var debugInfo = $"Member: {conceptMember.ValueType.Name}:'{conceptMember.Name}', Value: '{value}'.";
            result.MemberDebug[type].Add(debugInfo);
        }

        private CodeAnalysisError CreateAnalysisError(DslParseSyntaxException e)
        {
            var lineChr = textDocument.GetLineChr(e.Position);
            return new CodeAnalysisError() {LineChr = lineChr, Message = e.SimpleMessage};
        }

        private void OnUpdateContext(ITokenReader iTokenReader, Stack<IConceptInfo> context, bool isOpening)
        {
            var tokenReader = (TokenReader)iTokenReader;
            var lastToken = result.Tokens[tokenReader.PositionInTokenList - 1];
            var contextPos = lastToken.PositionInDslScript + lastToken.Value.Length;
            if (contextPos <= targetPos)
                result.ConceptContext = context.Reverse().ToList();
        }

        private void OnKeyword(ITokenReader iTokenReader, string keyword)
        {
            var tokenReader = (TokenReader)iTokenReader;
            if (tokenReader.PositionInTokenList >= result.Tokens.Count) return;

            var lastToken = result.Tokens[tokenReader.PositionInTokenList];
            if (lastToken.PositionInDslScript <= targetPos)
            {
                if (keyword != null)
                {
                    result.KeywordToken = lastToken;
                    result.MemberDebug = new Dictionary<string, List<string>>();
                    result.ValidConcepts = new List<IConceptInfo>();
                }
                else
                {
                    result.KeywordToken = null;
                }
            }
            else if (result.NextKeywordToken == null)
            {
                result.NextKeywordToken = lastToken;
            }
        }

        // Tokenizer just skips comments, so we are unable to detect whether typing is done inside a comment 
        // Therefore we need to reparse and detect all comment tokens
        private List<Token> ParseCommentTokens()
        {
            var script = textDocument.DslScripts.Single();
            var commentTokens = new List<Token>();
            try
            {
                var scriptPosition = 0;
                while (true)
                {
                    TokenizerInternals.SkipWhitespaces(script.Script, ref scriptPosition);
                    if (scriptPosition >= textDocument.Text.Length)
                        break;

                    var startPosition = scriptPosition;
                    var token = TokenizerInternals.GetNextToken_ValueType(script, ref scriptPosition, _ => "");
                    token.DslScript = script;
                    token.PositionInDslScript = startPosition;

                    if (token.Type == TokenType.Comment)
                        commentTokens.Add(token);
                }
            }
            catch
            {
                // we will ignore all errors as any relevant ones are capture by CreateTokenizerWithCapturedErrors()
            }

            return commentTokens;
        }

        // Due to unusual way the tokenizer works, if we capture errors during initial call to GetToken(),
        // valid tokens will be returned without error in subsequent calls
        private (Tokenizer tokenizer, List<CodeAnalysisError> capturedErrors) CreateTokenizerWithCapturedErrors()
        {
            var capturedErrors = new List<CodeAnalysisError>();
            var tokenizer = new Tokenizer(textDocument, new FilesUtility(rhetosLogProvider));
            try
            {
                _ = tokenizer.GetTokens();
            }
            catch (DslParseSyntaxException e)
            {
                var lineChr = textDocument.GetLineChr(e.Position);
                capturedErrors.Add(new CodeAnalysisError() { LineChr = lineChr, Message = e.SimpleMessage });
            }
            catch (Exception e)
            {
                capturedErrors.Add(new CodeAnalysisError() { Message = e.Message });
            }
            return (tokenizer, capturedErrors);
        }

    }
}
