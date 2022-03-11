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
using Rhetos.Dsl;

namespace Rhetos.LanguageServices.CodeAnalysis.Parsing
{
    public class CodeAnalysisResult
    {
        public int Line { get; }
        public int Chr { get; }
        public TextDocument TextDocument { get; }
        public bool SuccessfulRun { get; set; }
        public List<Token> Tokens { get; set; } = new List<Token>();
        public List<string> NonKeywordWords { get; set; }
        public List<Token> CommentTokens { get; set; } = new List<Token>();
        public List<ConceptSyntaxNode> ConceptContext { get; set; } = new List<ConceptSyntaxNode>();
        public Token KeywordToken { get; set; }
        public bool IsInsideComment { get; set; }
        public Token NextKeywordToken { get; set; }
        public List<CodeAnalysisError> TokenizerErrors { get; } = new List<CodeAnalysisError>();
        public List<CodeAnalysisError> DslParserErrors { get; } = new List<CodeAnalysisError>();
        public IEnumerable<CodeAnalysisError> AllErrors => TokenizerErrors.Concat(DslParserErrors);

        public List<ConceptType> ActiveConceptValidTypes { get; } = new List<ConceptType>();
        public Dictionary<ConceptType, ConceptMemberSyntax> LastMemberReadAttempt { get; } = new Dictionary<ConceptType, ConceptMemberSyntax>();
        public Dictionary<ConceptType, Token> LastTokenParsed { get; } = new Dictionary<ConceptType, Token>();

        public CodeAnalysisResult(TextDocument textDocument, int line, int chr)
        {
            this.TextDocument = textDocument;
            this.Line = line;
            this.Chr = chr;
        }

        public List<(ConceptType conceptType, int activeParamater)> GetValidConceptsWithActiveParameter()
        {
            return ActiveConceptValidTypes
                .Select(conceptType => (conceptType, GetActiveParameterForValidConcept(conceptType)))
                .OrderBy(a => a.conceptType.TypeName)
                .ToList();
        }

        private int GetActiveParameterForValidConcept(ConceptType conceptType)
        {
            var activeParameter = 0;

            // we have parsed some members successfully for this concept type
            if (LastTokenParsed.ContainsKey(conceptType))
            {
                activeParameter = ConceptTypeTools.IndexOfParameter(conceptType, LastMemberReadAttempt[conceptType]);

                // if we have just typed a keyword OR have stopped typing a parameter (by pressing space, etc.), we need to advance to next parameter
                // keyword scenario is possible in nested concepts, where we already have valid parameters and are just typing a keyword
                var lineChr = new LineChr(Line, Chr);
                var atLastParsed = GetTokenAtPosition(lineChr) == LastTokenParsed[conceptType] || GetTokenLeftOfPosition(lineChr) == LastTokenParsed[conceptType];
                var atKeyword = string.Equals(conceptType.Keyword, LastTokenParsed[conceptType].Value, StringComparison.InvariantCultureIgnoreCase);
                if (atKeyword || !atLastParsed)
                    activeParameter++;
            }

            return activeParameter;
        }

        public bool IsAfterAnyErrorPosition(LineChr lineChr)
        {
            var pos = TextDocument.GetPosition(lineChr);
            return AllErrors.Any(error => pos > TextDocument.GetPosition(error.BeginLineChr));
        }

        public bool IsAfterAnyErrorLine(LineChr lineChr)
        {
            return AllErrors.Where(a => a.Severity == CodeAnalysisError.ErrorSeverity.Error).Any(error => lineChr.Line > error.BeginLineChr.Line);
        }

        public Token GetTokenBeingTypedAtCursor(LineChr lineChr)
        {
            if (GetTokenAtPosition(lineChr) != null)
                return null;

            return GetTokenLeftOfPosition(lineChr);
        }

        public Token GetTokenAtPosition(LineChr lineChr)
        {
            var position = TextDocument.GetPosition(lineChr);

            foreach (var token in Tokens)
            {
                if (token.Type != TokenType.EndOfFile && position >= token.PositionInDslScript && position < token.PositionEndInDslScript)
                    return token;
            }

            return null;
        }

        public Token GetTokenLeftOfPosition(LineChr lineChr)
        {
            if (lineChr.Chr > 0) lineChr = new LineChr(lineChr.Line, lineChr.Chr - 1);
            return GetTokenAtPosition(lineChr);
        }
    }
}
