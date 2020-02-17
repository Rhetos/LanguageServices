using System;
using System.Collections.Generic;
using System.Linq;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class CodeAnalysisResult
    {
        public int Line { get; }
        public int Chr { get; }
        public TextDocument TextDocument { get; }
        public bool SuccessfulRun { get; set; }
        public List<Token> Tokens { get; set; }
        public List<Token> CommentTokens { get; set; }
        public List<IConceptInfo> ConceptContext { get; set; } = new List<IConceptInfo>();
        public Token KeywordToken { get; set; }
        public bool IsInsideComment { get; set; }
        public Token NextKeywordToken { get; set; }
        public List<CodeAnalysisError> TokenizerErrors { get; } = new List<CodeAnalysisError>();
        public List<CodeAnalysisError> DslParserErrors { get; } = new List<CodeAnalysisError>();
        public IEnumerable<CodeAnalysisError> AllErrors => TokenizerErrors.Concat(DslParserErrors);

        public Dictionary<string, List<string>> MemberDebug = new Dictionary<string, List<string>>();
        public List<IConceptInfo> ValidConcepts = new List<IConceptInfo>();
        public Dictionary<Type, ConceptMember> LastMemberReadAttempt = new Dictionary<Type, ConceptMember>();
        public Dictionary<Type, Token> LastTokenParsed = new Dictionary<Type, Token>();

        public CodeAnalysisResult(TextDocument textDocument, int line, int chr)
        {
            this.TextDocument = textDocument;
            this.Line = line;
            this.Chr = chr;
        }

        public List<(IConceptInfo concept, int activeParamater)> GetValidConceptsWithActiveParameter()
        {
            var lineChr = new LineChr(Line, Chr);
            var result = new List<(IConceptInfo concept, int activeParamater)>();
            foreach (var concept in ValidConcepts)
            {
                if (!LastTokenParsed.ContainsKey(concept.GetType()))
                {
                    result.Add((concept, 0));
                }
                else
                {
                    var atLastParsed = GetTokenAtPosition(lineChr) == LastTokenParsed[concept.GetType()] || GetTokenLeftOfPosition(lineChr) == LastTokenParsed[concept.GetType()];
                    var active = ConceptInfoType.IndexOfParameter(concept.GetType(), LastMemberReadAttempt[concept.GetType()]);
                    if (!atLastParsed || string.Equals(ConceptInfoHelper.GetKeyword(concept.GetType()), LastTokenParsed[concept.GetType()].Value, StringComparison.InvariantCultureIgnoreCase))
                        active++;
                    result.Add((concept, active));
                }
            }

            return result;
        }

        public bool IsAfterAnyErrorPosition(LineChr lineChr)
        {
            var pos = TextDocument.GetPosition(lineChr);
            return AllErrors.Any(error => pos > TextDocument.GetPosition(error.LineChr));
        }

        public bool IsAfterAnyErrorLine(LineChr lineChr)
        {
            return AllErrors.Any(error => lineChr.Line > error.LineChr.Line);
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
                if (position >= token.PositionInDslScript && position < token.PositionInDslScript + token.Value.Length)
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
