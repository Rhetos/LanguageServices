using System;
using System.Collections.Generic;
using System.Text;
using Rhetos.Dsl;

namespace Rhetos.LanguageServices.CodeAnalysis.Parsing
{
    public class TokenizerExplicitTokens : ITokenizer
    {
        private readonly List<Token> tokens;
        
        public TokenizerExplicitTokens(List<Token> tokens)
        {
            this.tokens = tokens;
        }

        public TokenizerResult GetTokens()
        {
            return new TokenizerResult()
            {
                Tokens = tokens,
                SyntaxError = null,
            };
        }
    }
}
