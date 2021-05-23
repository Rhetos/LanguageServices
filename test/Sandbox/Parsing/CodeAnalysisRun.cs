/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Extensions.Logging;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;
using Rhetos.Utilities;
using Sandbox;
using Sandbox.Parsing;

namespace Rhetos.LanguageServices.Server.Parsing
{
    internal class CodeAnalysisRun
    {
        private CodeAnalysisResult result;
        private int targetPos;
        private Token lastTokenBeforeTarget;
        private readonly TextDocument fullTextDocument;
        private readonly RhetosProjectContext rhetosProjectContext;
        private TextDocument textDocument;
        private Tokenizer tokenizer;
        private readonly ILogProvider rhetosLogProvider;

        public CodeAnalysisRun(TextDocument textDocument, RhetosProjectContext rhetosProjectContext, ILoggerFactory logFactory)
        {
            this.fullTextDocument = textDocument;
            this.rhetosProjectContext = rhetosProjectContext;
            this.rhetosLogProvider = new ConsoleLoggerProvider();
        }

        public CodeAnalysisResult RunForDocument()
        {
            return RunForPosition(null);
        }

        public CodeAnalysisResult RunForPosition(LineChr? lineChr)
        {
            InitializeResult(lineChr);
            InitializeTokenizers();

            // run-scoped variables needed for parse and callbacks
            targetPos = textDocument.GetPosition(result.Line, result.Chr);
            lastTokenBeforeTarget = result.Tokens.LastOrDefault(token => token.PositionInDslScript <= targetPos - 1);

            ParseAndCaptureErrors();
            ApplyCommentsToResult();

            result.SuccessfulRun = true;
            return result;
        }

        private void InitializeResult(LineChr? lineChr)
        {
            if (result != null) 
                throw new InvalidOperationException("Analysis already run.");

            if (lineChr == null)
                textDocument = fullTextDocument;
            else
                textDocument = new TextDocument(fullTextDocument.GetTruncatedAtNextEndOfLine(lineChr.Value), fullTextDocument.Uri);

            result = lineChr == null
                ? new CodeAnalysisResult(textDocument, 0, 0)
                : new CodeAnalysisResult(textDocument, lineChr.Value.Line, lineChr.Value.Chr);
        }

        private void InitializeTokenizers()
        {
            var (createdTokenizer, capturedErrors) = CreateTokenizerWithCapturedErrors();
            tokenizer = createdTokenizer;
            result.Tokens = tokenizer.GetTokens();
            result.TokenizerErrors.AddRange(capturedErrors);

            result.CommentTokens = ParseCommentTokens();
            result.NonKeywordWords = NonKeywordWordsFromTokens(result.Tokens);
        }

        private List<string> NonKeywordWordsFromTokens(List<Token> tokens)
        {
            return tokens
                .Where(token => token.Type == TokenType.Text && !token.Value.Contains(" "))
                .Select(token => token.Value)
                .Distinct()
                .Except(rhetosProjectContext.Keywords.Keys)
                .OrderBy(word => word)
                .ToList();
        }

        private void ParseAndCaptureErrors()
        {
            var dslParser = new DslParser(tokenizer, rhetosProjectContext.DslSyntax, rhetosLogProvider);

            try
            {
                dslParser.OnKeyword += OnKeyword;
                dslParser.OnMemberRead += OnMemberRead;
                dslParser.OnUpdateContext += OnUpdateContext;
                _ = dslParser.ParsedConcepts;
            }
            catch (DslSyntaxException e)
            {
                result.DslParserErrors.Add(CreateAnalysisError(e));
            }
            catch (Exception e)
            {
                result.DslParserErrors.Add(new CodeAnalysisError() { LineChr = LineChr.Zero, Message = e.Message });
            }
            finally
            {
                dslParser.OnKeyword -= OnKeyword;
                dslParser.OnMemberRead -= OnMemberRead;
                dslParser.OnUpdateContext -= OnUpdateContext;
            }
        }

        private void ApplyCommentsToResult()
        {
            Token lastCommentTokenBeforeTarget = null;
            foreach (var commentToken in result.CommentTokens)
            {
                if (targetPos >= commentToken.PositionInDslScript && targetPos < commentToken.PositionEndInDslScript)
                {
                    result.KeywordToken = null;
                    result.IsInsideComment = true;
                    break;
                }

                if (commentToken.PositionInDslScript > targetPos)
                    break;

                lastCommentTokenBeforeTarget = commentToken;
            }

            // handle situation where position is at the EOL after the comment
            if (lastCommentTokenBeforeTarget != null)
            {
                var lastTokenLine = textDocument.GetLineChr(lastCommentTokenBeforeTarget.PositionInDslScript).Line;
                if (lastTokenLine == result.Line)
                {
                    result.KeywordToken = null;
                    result.IsInsideComment = true;
                }
            }
        }

        private void OnMemberRead(ITokenReader iTokenReader, ConceptSyntaxNode conceptInfo, ConceptMemberSyntax conceptMember, ValueOrError<object> valueOrError)
        {
            // have we reached a new keyword after target pos? if so, prevent further member parsing
            if (result.NextKeywordToken != null)
                return;

            var tokenReader = (TokenReader)iTokenReader;
            if (tokenReader.PositionInTokenList <= 0 || lastTokenBeforeTarget == null)
                return;

            var concept = conceptInfo.Concept;
            var lastTokenRead = result.Tokens[tokenReader.PositionInTokenList - 1];

            // track last tokens/members parsed before or at target
            if (lastTokenRead.PositionInDslScript <= lastTokenBeforeTarget.PositionInDslScript && !valueOrError.IsError)
            {
                result.LastTokenParsed[concept] = lastTokenRead;
                result.LastMemberReadAttempt[concept] = conceptMember;
            }

            // we are interested in those concepts whose member parsing stops at or after target position
            if (lastTokenRead.PositionInDslScript >= lastTokenBeforeTarget.PositionInDslScript && !result.ActiveConceptValidTypes.Contains(concept))
                result.ActiveConceptValidTypes.Add(concept);
        }

        private CodeAnalysisError CreateAnalysisError(DslSyntaxException e)
        {
            var lineChr = new LineChr(e.FilePosition.BeginLine - 1, e.FilePosition.BeginColumn - 1);
            return new CodeAnalysisError() {LineChr = lineChr, Message = e.Message};
        }

        private void OnUpdateContext(ITokenReader iTokenReader, Stack<ConceptSyntaxNode> context, bool isOpening)
        {
            var tokenReader = (TokenReader)iTokenReader;
            var lastToken = result.Tokens[tokenReader.PositionInTokenList - 1];
            var contextPos = lastToken.PositionEndInDslScript;
            if (contextPos <= targetPos)
            {
                result.ConceptContext = context.Reverse().ToList();
                result.KeywordToken = null;
            }
        }

        private void OnKeyword(ITokenReader iTokenReader, string keyword)
        {
            var tokenReader = (TokenReader)iTokenReader;
            if (tokenReader.PositionInTokenList >= result.Tokens.Count) return;

            var lastToken = result.Tokens[tokenReader.PositionInTokenList];

            if (keyword == null && tokenReader.PositionInTokenList > 0)
                lastToken = result.Tokens[tokenReader.PositionInTokenList - 1];

            if (lastToken.PositionInDslScript <= targetPos)
            {
                if (keyword != null)
                {
                    result.KeywordToken = lastToken;
                    result.ActiveConceptValidTypes.Clear();
                    result.LastTokenParsed.Clear();
                    result.LastMemberReadAttempt.Clear();
                }
                else if (targetPos > lastToken.PositionInDslScript)
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
            var tokenizerInternals = new TokenizerInternals(rhetosProjectContext.DslSyntax);
            try
            {
                var scriptPosition = 0;
                while (true)
                {
                    TokenizerInternals.SkipWhitespaces(script.Script, ref scriptPosition);
                    if (scriptPosition >= textDocument.Text.Length)
                        break;

                    var startPosition = scriptPosition;
                    var token = tokenizerInternals.GetNextToken_ValueType(script, ref scriptPosition, _ => "");
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
            var safeTokenizer = new Tokenizer(textDocument, new FilesUtility(rhetosLogProvider), rhetosProjectContext.DslSyntax);
            try
            {
                safeTokenizer.GetTokens();
            }
            catch (DslSyntaxException e)
            {
                var lineChr = new LineChr(e.FilePosition.BeginLine - 1, e.FilePosition.BeginColumn - 1);
                capturedErrors.Add(new CodeAnalysisError() { LineChr = lineChr, Message = e.Message });
            }
            catch (Exception e)
            {
                capturedErrors.Add(new CodeAnalysisError() { Message = e.Message });
            }
            return (safeTokenizer, capturedErrors);
        }
    }
}
