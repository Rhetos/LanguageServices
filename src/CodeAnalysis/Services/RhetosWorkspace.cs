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
                    throw new InvalidOperationException($"No document with uri '{documentUri}' found in workspace.");

                return rhetosDocument;
            }
        }

        private bool lastProjectConfigurationDirtyStatus;
        
        public void UpdateRhetosContextStatus()
        {
            throw new NotImplementedException();
            /*
            lock (rhetosDocuments)
            {
                if (rhetosProjectContext.IsInitialized)
                {
                    rhetosAppContext.UpdateProjectConfigurationDirtyStatus();
                    if (lastProjectConfigurationDirtyStatus != rhetosAppContext.ProjectConfigurationDirty)
                    {
                        foreach (var rhetosDocument in rhetosDocuments)
                        {
                            rhetosDocument.Value.InvalidateAnalysisCache();
                            documentChangeTimes[rhetosDocument.Key] = DateTime.Now;
                        }
                    }
                    lastProjectConfigurationDirtyStatus = rhetosAppContext.ProjectConfigurationDirty;
                    return;
                }

                var documentsWithValidRootPath = rhetosDocuments.Values
                    .Where(document => !string.IsNullOrEmpty(document.RootPathConfiguration?.RootPath))
                    .ToList();
                    
                // try to initialize with each open document with valid rootPath
                foreach (var rhetosDocument in documentsWithValidRootPath)
                {
                    log.LogTrace($"Trying to initialize context with rootPath='{rhetosDocument.RootPathConfiguration.RootPath}' from document '{rhetosDocument.DocumentUri}'.");
                    rhetosAppContext.InitializeFromRhetosProjectPath(rhetosDocument.RootPathConfiguration.RootPath);

                    if (rhetosDocument.RhetosAppContextInitializeError?.Message != rhetosAppContext.LastInitializeError?.Message)
                    {
                        rhetosDocument.RhetosAppContextInitializeError = rhetosAppContext.LastInitializeError;
                        rhetosDocument.InvalidateAnalysisCache();
                        documentChangeTimes[rhetosDocument.DocumentUri] = DateTime.Now;
                    }

                    if (rhetosProjectContext.IsInitialized) break;
                }

                if (!rhetosProjectContext.IsInitialized) return;

                // we have just initialized, reset all documents' analysis states
                foreach (var rhetosDocument in documentsWithValidRootPath)
                {
                    log.LogTrace($"Reset code analysis for {rhetosDocument.DocumentUri}. LastError = {rhetosAppContext.LastInitializeError?.Message}");
                    rhetosDocument.RhetosAppContextInitializeError = null;
                    rhetosDocument.InvalidateAnalysisCache();
                    documentChangeTimes[rhetosDocument.DocumentUri] = DateTime.Now;
                }
            }*/
        }
    }
}
