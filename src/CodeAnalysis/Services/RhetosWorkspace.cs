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
        private readonly Dictionary<Uri, RhetosDocument> rhetosDocuments = new Dictionary<Uri, RhetosDocument>();
        private readonly Dictionary<Uri, DateTime> documentChangeTimes = new Dictionary<Uri, DateTime>();
        private readonly ILogger<RhetosWorkspace> log;
        private readonly RhetosDocumentFactory rhetosDocumentFactory;
        private readonly RhetosProjectContext rhetosProjectContext;

        public RhetosWorkspace(RhetosDocumentFactory rhetosDocumentFactory, RhetosProjectContext rhetosProjectContext, ILogger<RhetosWorkspace> log)
        {
            this.log = log;
            this.rhetosDocumentFactory = rhetosDocumentFactory;
            this.rhetosProjectContext = rhetosProjectContext;
        }

        public List<Uri> GetUpdatedDocuments(DateTime sinceTime)
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

        public List<Uri> GetClosedDocuments(DateTime sinceTime)
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

        public void UpdateDocumentText(Uri documentUri, string text)
        {
            lock (rhetosDocuments)
            {
                if (!rhetosDocuments.TryGetValue(documentUri, out var rhetosDocument))
                {
                    rhetosDocument = rhetosDocumentFactory.CreateNew(documentUri);
                    rhetosDocuments.Add(documentUri, rhetosDocument);
                }

                rhetosDocument.UpdateText(text);
                documentChangeTimes[documentUri] = DateTime.Now;
            }
        }

        public void CloseDocument(Uri documentUri)
        {
            lock (rhetosDocuments)
            {
                documentChangeTimes[documentUri] = DateTime.Now;

                if (rhetosDocuments.ContainsKey(documentUri))
                    rhetosDocuments.Remove(documentUri);
            }
        }

        public RhetosDocument GetRhetosDocument(Uri documentUri)
        {
            lock (rhetosDocuments)
            {
                if (!rhetosDocuments.TryGetValue(documentUri, out var rhetosDocument))
                    throw new InvalidOperationException($"No document with uri '{documentUri.AbsolutePath}' found in workspace.");

                return rhetosDocument;
            }
        }
       
        public void UpdateRhetosProjectContext()
        {
            lock (rhetosDocuments)
            {
                var contextUpdateTime = rhetosProjectContext.LastContextUpdateTime;
                UpdateProjectContextRootPath();

                if (rhetosProjectContext.IsInitialized)
                    rhetosProjectContext.UpdateDslSyntax();

                if (rhetosProjectContext.LastContextUpdateTime == contextUpdateTime)
                    return;
                
                foreach (var (uri, rhetosDocument) in rhetosDocuments)
                {
                    log.LogTrace($"Reset code analysis for {uri.AbsolutePath}.");
                    rhetosDocument.InvalidateAnalysisCache();
                    documentChangeTimes[uri] = DateTime.Now;
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
                    rhetosProjectContext.Initialize(new DslSyntaxProvider(rootPath));

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
