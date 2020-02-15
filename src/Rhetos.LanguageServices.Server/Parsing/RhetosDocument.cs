using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class RhetosDocument
    {
        public TextDocument TextDocument { get; private set; }
        private readonly RhetosAppContext rhetosAppContext;
        private readonly object _syncAnalysis = new object();
        private readonly ILoggerFactory logFactory;
        private readonly ConceptQueries conceptQueries;

        public RhetosDocument(RhetosAppContext rhetosAppContext, ConceptQueries conceptQueries, ILoggerFactory logFactory)
        {
            this.rhetosAppContext = rhetosAppContext;
            this.logFactory = logFactory;
            this.conceptQueries = conceptQueries;
            UpdateText("");
        }

        public void UpdateText(string text)
        {
            TextDocument = new TextDocument(text);
        }

        public CodeAnalysisResult GetAnalysis()
            => GetAnalysis(null);

        public CodeAnalysisResult GetAnalysis(LineChr? lineChr)
        {
            lock (_syncAnalysis)
            {
                // gracefully return empty analysis if RhetosAppContext is not yet initialized
                if (!rhetosAppContext.IsInitialized) return new CodeAnalysisResult(TextDocument, 0, 0);

                var relevantText = TextDocument;
                if (lineChr != null)
                    relevantText = new TextDocument(TextDocument.GetTruncatedAtNextEndOfLine(lineChr.Value));

                var analysisRun = new CodeAnalysisRun(relevantText, rhetosAppContext, logFactory);
                return analysisRun.RunForPosition(lineChr);
            }
        }

        public List<string> GetCompletionKeywordsAtPosition(LineChr lineChr)
        {
            var analysisResult = GetAnalysis(lineChr);

            if (analysisResult.IsInsideComment)
                return new List<string>();

            var typingToken = analysisResult.GetTokenBeingTypedAtCursor(lineChr);
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
        
        public (string description, LineChr startPosition, LineChr endPosition) GetHoverDescriptionAtPosition(LineChr lineChr)
        {
            var analysis = GetAnalysis(lineChr);
            if (analysis.KeywordToken == null)
                return (null, LineChr.Zero, LineChr.Zero);

            var description = conceptQueries.GetFullDescription(analysis.KeywordToken.Value);
            if (string.IsNullOrEmpty(description))
                description = $"No documentation found for '{analysis.KeywordToken.Value}'.";

            var startPosition = TextDocument.GetLineChr(analysis.KeywordToken.PositionInDslScript);

            var endPosition = lineChr;
            if (analysis.NextKeywordToken != null)
                endPosition = TextDocument.GetLineChr(analysis.NextKeywordToken.PositionInDslScript - 1);

            return (description, startPosition, endPosition);
        }
    }
}
