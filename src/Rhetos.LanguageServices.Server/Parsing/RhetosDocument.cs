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
using Microsoft.Extensions.Logging;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Parsing
{
    public class RhetosDocument
    {
        public TextDocument TextDocument { get; private set; }
        public Uri DocumentUri { get; }
        public RootPathConfiguration RootPathConfiguration { get; private set; }
        private readonly RhetosAppContext rhetosAppContext;
        // we don't want to run analysis during document text change and also rely on Rhetos parsing infrastructure to be thread-safe
        private static readonly object _syncAnalysis = new object(); 
        private readonly ILoggerFactory logFactory;
        private readonly ConceptQueries conceptQueries;
        private readonly Dictionary<int, CodeAnalysisResult> cachedAnalysisResults = new Dictionary<int, CodeAnalysisResult>();

        public RhetosDocument(RhetosAppContext rhetosAppContext, ConceptQueries conceptQueries, ILoggerFactory logFactory, Uri documentUri)
        {
            this.rhetosAppContext = rhetosAppContext;
            this.conceptQueries = conceptQueries;
            this.logFactory = logFactory;
            this.DocumentUri = documentUri;
            UpdateText("");
        }

        public void UpdateText(string text)
        {
            lock (_syncAnalysis)
            {
                TextDocument = new TextDocument(text, DocumentUri);
                cachedAnalysisResults.Clear();

                UpdateRootPathConfiguration();

                if (RootPathConfiguration?.RootPath != null && !rhetosAppContext.IsInitialized)
                    rhetosAppContext.InitializeFromAppPath(RootPathConfiguration.RootPath);
            }
        }

        private void UpdateRootPathConfiguration()
        {
            var directiveConfiguration = rhetosAppContext.GetRhetosAppRootPath(this, true);
            var deletedDirective = RootPathConfiguration?.ConfigurationType == RootPathConfigurationType.SourceDirective
                                   && directiveConfiguration.ConfigurationType == RootPathConfigurationType.None;

            // run full configuration scan if we have just deleted a source directive
            // OR we have never ran a full configuration for this document
            if (RootPathConfiguration == null || deletedDirective)
            {
                RootPathConfiguration = rhetosAppContext.GetRhetosAppRootPath(this);
            }
            // else just update directive configuration if present
            else if (directiveConfiguration.ConfigurationType == RootPathConfigurationType.SourceDirective)
            {
                RootPathConfiguration = directiveConfiguration;
            }
        }

        public CodeAnalysisResult GetAnalysis()
            => GetAnalysis(null);

        public CodeAnalysisResult GetAnalysis(LineChr? lineChr)
        {
            lock (_syncAnalysis)
            {
                var cacheKey = lineChr == null
                    ? -1
                    : TextDocument.GetPosition(lineChr.Value);

                if (cachedAnalysisResults.TryGetValue(cacheKey, out var cachedResult))
                    return cachedResult;

                var blockedAnalysisResult = BlockedAnalysisResult();
                if (blockedAnalysisResult != null) return blockedAnalysisResult;

                var analysisRun = new CodeAnalysisRun(TextDocument, rhetosAppContext, logFactory);
                var result = analysisRun.RunForPosition(lineChr);
                cachedAnalysisResults[cacheKey] = result;
                return result;
            }
        }

        private CodeAnalysisResult BlockedAnalysisResult()
        {
            // we will check document root path configuration only if appContext is not initialized from current domain
            // this allows for integration scenarios such as unit testing
            if (!rhetosAppContext.IsInitializedFromCurrentDomain)
            {
                var analysisResult = new CodeAnalysisResult(TextDocument, 0, 0);

                // if we have a failed initialization attempt, add it to error list
                if (rhetosAppContext.LastInitializeError != null)
                    analysisResult.DslParserErrors.Add(rhetosAppContext.LastInitializeError);

                // document doesn't have a valid root path configured
                if (string.IsNullOrEmpty(RootPathConfiguration?.RootPath))
                {
                    var error = string.IsNullOrEmpty(RootPathConfiguration?.Context)
                        ? ""
                        : $" ({RootPathConfiguration.Context})";
                    var message = $"No valid RhetosAppRoothPath configuration was found for this document{error}. "
                                  + "If document is in folder subtree of Rhetos application, it must be built at least once. "
                                  + "Otherwise, explicit paths may be set via '// <rhetosAppRootPath=\"PATH\" />' source code directive or "
                                  + "by using 'rhetos-language-services.settings.json' configuration file.";

                    analysisResult.DslParserErrors.Add(new CodeAnalysisError() { Message = message, Severity = CodeAnalysisError.ErrorSeverity.Warning });
                }
                // document's root path is different than path used to initialize RhetosAppContext
                else if (rhetosAppContext.IsInitialized && !string.Equals(rhetosAppContext.RootPath, RootPathConfiguration?.RootPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    var message = $"Language Services have been initialized with Rhetos app at '{rhetosAppContext.RootPath}'. "
                                  + $"This document is configured to use different Rhetos app at '{RootPathConfiguration.RootPath}'. No code analysis will be performed. "
                                  + "Restart Visual Studio if you want to use a different Rhetos app.";
                    analysisResult.DslParserErrors.Add(new CodeAnalysisError() { Message = message, Severity = CodeAnalysisError.ErrorSeverity.Warning });
                }

                return analysisResult.AllErrors.Any() 
                    ? analysisResult 
                    : null;
            }

            // gracefully return empty analysis if RhetosAppContext is not yet initialized
            if (!rhetosAppContext.IsInitialized) return new CodeAnalysisResult(TextDocument, 0, 0);

            return null;
        }

        public List<string> GetCompletionKeywordsAtPosition(LineChr lineChr)
        {
            var analysisResult = GetAnalysis(lineChr);

            if (analysisResult.IsInsideComment || analysisResult.IsAfterAnyErrorLine(lineChr))
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
                .OrderBy(keyword => keyword)
                .ToList();

            return keywords;
        }

        public (string description, LineChr startPosition, LineChr endPosition) GetHoverDescriptionAtPosition(LineChr lineChr)
        {
            var analysis = GetAnalysis(lineChr);
            if (analysis.KeywordToken == null || analysis.IsAfterAnyErrorLine(lineChr))
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

        public (List<RhetosSignature> signatures, int? activeSignature, int? activeParameter) GetSignatureHelpAtPosition(LineChr lineChr)
        {
            var analysis = GetAnalysis(lineChr);
            if (analysis.KeywordToken == null || analysis.IsAfterAnyErrorLine(lineChr))
                return (null, null, null);

            var signaturesWithDocumentation = conceptQueries.GetSignaturesWithDocumentation(analysis.KeywordToken.Value);
            var validConcepts = analysis.GetValidConceptsWithActiveParameter();

            if (!validConcepts.Any())
                return (signaturesWithDocumentation, null, null);

            var sortedConcepts = validConcepts
                .Select(valid =>
                (
                    valid.conceptType,
                    valid.activeParamater,
                    parameterCount: ConceptInfoType.GetParameters(valid.conceptType).Count,
                    documentation: signaturesWithDocumentation.Single(sig => sig.ConceptInfoType == valid.conceptType)
                ))
                .OrderBy(valid => valid.activeParamater >= valid.parameterCount)
                .ThenBy(valid => valid.parameterCount)
                .ThenBy(valid => valid.conceptType.Name)
                .ToList();

            var activeParameter = sortedConcepts.First().activeParamater;

            return (sortedConcepts.Select(sorted => sorted.documentation).ToList(), 0, activeParameter);
        }
    }
}
