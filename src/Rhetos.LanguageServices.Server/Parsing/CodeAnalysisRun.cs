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
                dslParser.ParseConceptsWithCallbacks(OnKeyword, OnMemberRead, OnUpdateContext);
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

        private void OnMemberRead(ITokenReader iTokenReader, IConceptInfo conceptInfo, ConceptMember conceptMember, ValueOrError<object> valueOrError)
        {
            var tokenReader = (TokenReader)iTokenReader;
            // if (tokenReader.PositionInTokenList >= tokens.Count) return;
            if (tokenReader.PositionInTokenList == 0) return;

            var lastToken = tokens[tokenReader.PositionInTokenList - 1];
            if (lastToken.PositionInDslScript > targetPos) return;

            var type = conceptInfo.GetType().Name;
            if (!result.MemberDebug.ContainsKey(type)) result.MemberDebug[type] = new List<string>();

            var value = valueOrError.IsError ? valueOrError.Error : valueOrError.Value.ToString();

            var debugInfo = $"Member: {conceptMember.ValueType.Name}:'{conceptMember.Name}', Value: '{value}'.";
            result.MemberDebug[type].Add(debugInfo);
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
            if (lastToken.PositionInDslScript <= targetPos)
            {
                if (keyword != null)
                {
                    result.KeywordToken = lastToken;
                    result.MemberDebug = new Dictionary<string, List<string>>();
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
    }
}
