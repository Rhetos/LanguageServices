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
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.CodeAnalysis.Tools;

namespace Rhetos.LanguageServices.CodeAnalysis.Parsing
{
    public class RhetosDocument
    {
        public TextDocument TextDocument { get; private set; }
        public Uri DocumentUri { get; }
        public RootPathConfiguration RootPathConfiguration { get; private set; }

        private readonly RhetosProjectContext rhetosProjectContext;
        // we don't want to run analysis during document text change and also rely on Rhetos parsing infrastructure to be thread-safe
        private static readonly object _syncAnalysis = new object();
        private readonly ILoggerFactory logFactory;
        private readonly ConceptQueries conceptQueries;
        private readonly IRhetosProjectRootPathResolver rhetosProjectRootPathResolver;
        private readonly Dictionary<int, CodeAnalysisResult> cachedAnalysisResults = new Dictionary<int, CodeAnalysisResult>();

        public RhetosDocument(RhetosProjectContext rhetosProjectContext, ConceptQueries conceptQueries, IRhetosProjectRootPathResolver rhetosProjectRootPathResolver,
            ILoggerFactory logFactory, Uri documentUri)
        {
            this.rhetosProjectContext = rhetosProjectContext;
            this.conceptQueries = conceptQueries;
            this.rhetosProjectRootPathResolver = rhetosProjectRootPathResolver;
            this.logFactory = logFactory;
            this.DocumentUri = documentUri;
            UpdateText("");
        }

        public void UpdateText(string text)
        {
            lock (_syncAnalysis)
            {
                TextDocument = new TextDocument(text, DocumentUri);
                InvalidateAnalysisCache();

                UpdateRootPathConfiguration();
            }
        }

        public void InvalidateAnalysisCache()
        {
            lock (_syncAnalysis)
            {
                cachedAnalysisResults.Clear();
            }
        }

        public bool UpdateRootPathIfChanged()
        {
            lock (_syncAnalysis)
            {
                var oldPath = RootPathConfiguration.RootPath;
                UpdateRootPathConfiguration();

                if (RootPathConfiguration.RootPath != oldPath)
                {
                    InvalidateAnalysisCache();
                    return true;
                }

                return false;
            }
        }

        private void UpdateRootPathConfiguration()
        {
            var directiveConfiguration = rhetosProjectRootPathResolver.ResolveRootPathFromDocumentDirective(this);
            var deletedDirective = RootPathConfiguration?.ConfigurationType == RootPathConfigurationType.SourceDirective
                                   && directiveConfiguration.ConfigurationType == RootPathConfigurationType.None;

            // run full configuration scan if we have just deleted a source directive
            // OR we have never ran a successful configuration for this document
            if (RootPathConfiguration?.RootPath == null || deletedDirective)
            {
                RootPathConfiguration = rhetosProjectRootPathResolver.ResolveRootPathForDocument(this);
            }
            // else just update directive configuration if present
            else if (directiveConfiguration?.ConfigurationType == RootPathConfigurationType.SourceDirective)
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

                var analysisRun = new CodeAnalysisRun(TextDocument, rhetosProjectContext, logFactory);
                var result = analysisRun.RunForPosition(lineChr);

                cachedAnalysisResults[cacheKey] = result;
                return result;
            }
        }

        private CodeAnalysisResult BlockedAnalysisResult()
        {
            var analysisResult = new CodeAnalysisResult(TextDocument, 0, 0);

            // if we have a failed initialization attempt, add it to error list
            if (rhetosProjectContext.InitializationError != null)
                analysisResult.DslParserErrors.Add(rhetosProjectContext.InitializationError);

            // document doesn't have a valid root path configured
            if (string.IsNullOrEmpty(RootPathConfiguration?.RootPath))
            {
                var error = string.IsNullOrEmpty(RootPathConfiguration?.Context)
                    ? ""
                    : $" ({RootPathConfiguration.Context})";
                var message = $"No valid RhetosProjectRootPath configuration was found for this document{error}. "
                              + "If document is in folder subtree of Rhetos application, it must be built successfully at least once. "
                              + "Otherwise, explicit paths may be set via '// <rhetosProjectRootPath=\"PATH\" />' source code directive or "
                              + "by using 'rhetos-language-services.settings.json' configuration file.";

                analysisResult.DslParserErrors.Add(new CodeAnalysisError() { Message = message, Severity = CodeAnalysisError.ErrorSeverity.Warning });
            }
            // there is a valid rootPath, but initialize has not been run yet, should be transient error until the next RhetosProjectMonitor cycle
            else if (!rhetosProjectContext.IsInitialized && rhetosProjectContext.InitializationError == null)
            {
                analysisResult.DslParserErrors.Add(new CodeAnalysisError() { Message = "Waiting for Rhetos Project initialization.", Severity = CodeAnalysisError.ErrorSeverity.Warning });
            }
            // document's root path is different than path used to initialize RhetosAppContext
            else if (rhetosProjectContext.IsInitialized && !string.Equals(rhetosProjectContext.ProjectRootPath, RootPathConfiguration?.RootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                var message = $"Language Services have been initialized with Rhetos app at '{rhetosProjectContext.ProjectRootPath}'. "
                              + $"This document is configured to use different Rhetos app at '{RootPathConfiguration.RootPath}'. No code analysis will be performed. "
                              + "Restart Visual Studio if you want to use a different Rhetos app.";
                analysisResult.DslParserErrors.Add(new CodeAnalysisError() { Message = message, Severity = CodeAnalysisError.ErrorSeverity.Warning });
            }
            
            return analysisResult.AllErrors.Any()
                ? analysisResult
                : null;
        }
        
        public List<string> GetCompletionKeywordsAtPosition(LineChr lineChr)
        {
            var analysisResult = GetAnalysis(lineChr);
            
            if (analysisResult.IsInsideComment || analysisResult.IsAfterAnyErrorLine(lineChr) || !analysisResult.SuccessfulRun)
                return new List<string>();

            var typingToken = analysisResult.GetTokenBeingTypedAtCursor(lineChr);
            if (analysisResult.KeywordToken != null && analysisResult.KeywordToken != typingToken)
            {
                var fullAnalysis = GetAnalysis();
                return fullAnalysis.NonKeywordWords;
            }

            var lastParent = analysisResult.ConceptContext.LastOrDefault();
            var validConcepts = lastParent == null
                ? rhetosProjectContext.DslSyntax.ConceptTypes.ToList()
                : conceptQueries.ValidConceptsForParent(lastParent.Concept);

            var keywords = validConcepts
                .Select(concept => concept.Keyword)
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
                    parameterCount: ConceptTypeTools.GetParameters(valid.conceptType).Count,
                    documentation: signaturesWithDocumentation.Single(sig => sig.ConceptType == valid.conceptType)
                ))
                .OrderBy(valid => valid.activeParamater >= valid.parameterCount)
                .ThenBy(valid => valid.parameterCount)
                .ThenBy(valid => valid.conceptType.TypeName)
                .ToList();

            var activeParameter = sortedConcepts.First().activeParamater;

            return (sortedConcepts.Select(sorted => sorted.documentation).ToList(), 0, activeParameter);
        }
    }
}
