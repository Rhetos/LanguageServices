using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Rhetos.LanguageServices.Server.Parsing;

namespace Rhetos.LanguageServices.Server.Services
{
    public class RhetosWorkspace
    {

        private readonly Dictionary<Uri, RhetosDocument> rhetosDocuments = new Dictionary<Uri, RhetosDocument>();
        private readonly Dictionary<Uri, DateTime> documentChangeTimes = new Dictionary<Uri, DateTime>();
        private readonly ILogger<RhetosWorkspace> log;
        private readonly RhetosDocumentFactory rhetosDocumentFactory;
        private readonly RhetosAppContext rhetosAppContext;

        public RhetosWorkspace(RhetosDocumentFactory rhetosDocumentFactory, RhetosAppContext rhetosAppContext, ILogger<RhetosWorkspace> log)
        {
            this.log = log;
            this.rhetosDocumentFactory = rhetosDocumentFactory;
            this.rhetosAppContext = rhetosAppContext;
        }

        public List<Uri> GetUpdatedDocuments(DateTime sinceTime)
        {
            lock (rhetosDocuments)
            {
                // if context has been changed in the meantime, report all documents as changed
                if (rhetosAppContext.LastContextUpdateTime > sinceTime)
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
    }
}
