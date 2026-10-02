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
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Tools;

namespace Rhetos.LanguageServices.CodeAnalysis.Services
{
    // TODO: this class is maybe specific for LanguageServices
    public class RhetosWorkspace
    {
        /// <summary>
        /// Comparer for document URI strings, used as keys of the open documents.
        /// It mirrors OmniSharp's DocumentUri equality: case-insensitive, except on Linux, where file paths are case-sensitive.
        /// For file URIs this also matches the former System.Uri keys.
        /// </summary>
        public static StringComparer DocumentUriComparer { get; } = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        // The keys are the URI strings from the first didOpen of each document, so that diagnostics are published with the opened URI.
        private readonly Dictionary<string, RhetosDocument> rhetosDocuments = new Dictionary<string, RhetosDocument>(DocumentUriComparer);
        private readonly Dictionary<string, DateTime> documentChangeTimes = new Dictionary<string, DateTime>(DocumentUriComparer);
        private readonly ILogger<RhetosWorkspace> log;
        private readonly ILoggerFactory logFactory;
        private readonly RhetosDocumentFactory rhetosDocumentFactory;
        private readonly RhetosProjectContext rhetosProjectContext;

        public RhetosWorkspace(RhetosDocumentFactory rhetosDocumentFactory, RhetosProjectContext rhetosProjectContext, ILogger<RhetosWorkspace> log, ILoggerFactory logFactory)
        {
            this.log = log;
            this.logFactory = logFactory;
            this.rhetosDocumentFactory = rhetosDocumentFactory;
            this.rhetosProjectContext = rhetosProjectContext;
        }

        /// <summary>
        /// Returns the URIs of open documents that were changed after <paramref name="sinceTime"/>.
        /// Each URI is the one the document was opened with.
        /// </summary>
        public List<string> GetUpdatedDocuments(DateTime sinceTime)
        {
            lock (rhetosDocuments)
            {
                // if context has been changed in the meantime, report all documents as changed
                if (rhetosProjectContext.LastContextUpdateTime > sinceTime)
                    return rhetosDocuments.Keys.ToList();

                return rhetosDocuments
                    .Where(document => documentChangeTimes[document.Key] > sinceTime)
                    .Select(document => document.Key)
                    .ToList();
            }
        }

        /// <summary>
        /// Returns the URIs of documents that were closed after <paramref name="sinceTime"/> and are not open again.
        /// </summary>
        public List<string> GetClosedDocuments(DateTime sinceTime)
        {
            lock (rhetosDocuments)
            {
                return documentChangeTimes
                    .Where(document => document.Value > sinceTime)
                    .Where(document => !rhetosDocuments.ContainsKey(document.Key))
                    .Select(document => document.Key)
                    .ToList();
            }
        }

        /// <summary>
        /// Opens the document if it is not open yet, and sets its text.
        /// If the document is already open (compared by <see cref="DocumentUriComparer"/>), it keeps the <see cref="DocumentKey"/> it was opened with.
        /// </summary>
        public void UpdateDocumentText(DocumentKey documentKey, string text)
        {
            ArgumentNullException.ThrowIfNull(documentKey);

            lock (rhetosDocuments)
            {
                if (!rhetosDocuments.TryGetValue(documentKey.Uri, out var rhetosDocument))
                {
                    rhetosDocument = rhetosDocumentFactory.CreateNew(documentKey);
                    rhetosDocuments.Add(documentKey.Uri, rhetosDocument);
                }

                rhetosDocument.UpdateText(text);
                SetDocumentChangeTime(rhetosDocument.DocumentKey.Uri);
            }
        }

        /// <summary>
        /// Closes the document. <see cref="GetClosedDocuments"/> reports it with the URI it was opened with.
        /// </summary>
        public void CloseDocument(string documentUri)
        {
            lock (rhetosDocuments)
            {
                if (rhetosDocuments.TryGetValue(documentUri, out var rhetosDocument))
                {
                    documentUri = rhetosDocument.DocumentKey.Uri;
                    rhetosDocuments.Remove(documentUri);
                }

                SetDocumentChangeTime(documentUri);
            }
        }

        /// <summary>
        /// Stores the change time with exactly this URI string as the key,
        /// replacing an existing entry that is equal by <see cref="DocumentUriComparer"/> but may differ in case.
        /// </summary>
        private void SetDocumentChangeTime(string documentUri)
        {
            documentChangeTimes.Remove(documentUri);
            documentChangeTimes.Add(documentUri, DateTime.Now);
        }

        /// <summary>
        /// Returns the open document with the given URI (compared by <see cref="DocumentUriComparer"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">The document is not open.</exception>
        public RhetosDocument GetRhetosDocument(string documentUri)
        {
            lock (rhetosDocuments)
            {
                if (!rhetosDocuments.TryGetValue(documentUri, out var rhetosDocument))
                    throw new InvalidOperationException($"No document with uri '{documentUri}' found in workspace.");

                return rhetosDocument;
            }
        }
       
        public void UpdateRhetosProjectContext()
        {
            lock (rhetosDocuments)
            {
                var contextUpdateTime = rhetosProjectContext.LastContextUpdateTime;

                if (rhetosProjectContext.IsInitialized)
                    rhetosProjectContext.UpdateDslSyntax();

                UpdateProjectContextRootPath();

                if (rhetosProjectContext.LastContextUpdateTime == contextUpdateTime)
                    return;
                
                foreach (var (uri, rhetosDocument) in rhetosDocuments)
                {
                    log.LogTrace($"Reset code analysis for {uri}.");
                    rhetosDocument.InvalidateAnalysisCache();
                    SetDocumentChangeTime(uri);
                }
            }
        }

        private void UpdateProjectContextRootPath()
        {
            lock (rhetosDocuments)
            {
                // each document has to refresh its root path in case there wasn't a valid path (not yet compiled rhetos app) and in the meantime it was created
                // this ensures that in such scenario new available valid paths for documents are considered
                foreach (var rhetosDocument in rhetosDocuments.Values)
                    rhetosDocument.UpdateRootPathIfChanged();

                var documentRootPaths = GetRootPathsFromDocuments();

                if (documentRootPaths.Count == 0)
                    return;

                if (rhetosProjectContext.IsInitialized && documentRootPaths.Contains(rhetosProjectContext.ProjectRootPath))
                    return;

                if (rhetosProjectContext.IsInitialized)
                    log.LogDebug($"Changing current rootPath='{rhetosProjectContext.ProjectRootPath}' since it is no longer used.");

                TryInitializeWithRootPaths(documentRootPaths);
            }
        }

        private void TryInitializeWithRootPaths(IEnumerable<string> rootPaths)
        {
            foreach (var rootPath in rootPaths)
            {
                try
                {
                    if (!DslSyntaxProvider.IsValidProjectRootPath(rootPath))
                    {
                        log.LogTrace($"Path '{rootPath}' is not a valid Rhetos Project path, skipping.");
                        continue;
                    }
                        
                    log.LogTrace($"Trying to initialize RhetosProjectContext with rootPath='{rootPath}'.");
                    rhetosProjectContext.Initialize(new DslSyntaxProvider(rootPath, new RhetosNetCoreLogProvider(logFactory)));

                    if (rhetosProjectContext.ProjectRootPath == rootPath)
                        break;
                }
                catch (Exception e)
                {
                    log.LogTrace($"RhetosProjectContext initialize failed at rootPath='{rootPath}'. {e}");
                }
            }
        }

        private List<string> GetRootPathsFromDocuments()
        {
            lock (rhetosDocuments)
            {
                var rootPaths = rhetosDocuments.Values
                    .Select(a => a.RootPathConfiguration?.RootPath)
                    .Where(a => !string.IsNullOrEmpty(a))
                    .GroupBy(a => a)
                    .Select(a => (rootPath: a.Key, count: a.Count()))
                    .OrderByDescending(a => a.count)
                    .Select(a => a.rootPath)
                    .ToList();

                return rootPaths;
            }
        }
    }
}
