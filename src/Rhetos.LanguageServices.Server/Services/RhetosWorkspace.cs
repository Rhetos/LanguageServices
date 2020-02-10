using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NLog;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Rhetos.LanguageServices.Server.Services
{
    public class RhetosWorkspace
    {
        public Dictionary<string, DateTime> DocumentChangeTimes { get; } = new Dictionary<string, DateTime>();

        private readonly Dictionary<string, RhetosDocument> rhetosDocuments = new Dictionary<string, RhetosDocument>();
        private readonly ILogger<RhetosWorkspace> log;
        private readonly RhetosDocumentFactory rhetosDocumentFactory;

        public RhetosWorkspace(RhetosDocumentFactory rhetosDocumentFactory, ILogger<RhetosWorkspace> log)
        {
            this.log = log;
            this.rhetosDocumentFactory = rhetosDocumentFactory;
        }

        public void UpdateDocumentText(Uri documentUri, string text)
            => UpdateDocumentText(documentUri.AbsoluteUri, text);

        public void UpdateDocumentText(string id, string text)
        {
            var rhetosDocument = GetRhetosDocument(id);
            if (rhetosDocument == null)
            {
                rhetosDocument = rhetosDocumentFactory.CreateNew();
                rhetosDocuments.Add(id, rhetosDocument);
            }

            rhetosDocument.UpdateText(text);
            DocumentChangeTimes[id] = DateTime.Now;
        }

        public RhetosDocument GetRhetosDocument(Uri documentUri)
            => GetRhetosDocument(documentUri.AbsoluteUri);

        public RhetosDocument GetRhetosDocument(string id)
        {
            if (!rhetosDocuments.TryGetValue(id, out var rhetosDocument))
                return null;

            return rhetosDocument;
        }

        /*
        private void PendingProcessLoop()
        {
            log.LogTrace($"New ProcessLoop started.");
            while (documentTextUpdates.Any())
            {
                Task.Delay(100).Wait();
                var elapsed = DateTime.Now - lastDocumentChangeTime;
                if (elapsed < TimeSpan.FromMilliseconds(300)) continue;
                
                RunAnalysis();
            }
            log.LogTrace($"ProcessLoop completed.");
        }

        private void RunAnalysis()
        {
            log.LogInformation($"Running document analysis.");
            var documentsToProcess = documentTextUpdates.Keys.ToList();
            foreach (var documentUri in documentsToProcess)
            {
                if (documentTextUpdates.TryRemove(documentUri, out var text))
                {
                    var rhetosDocument = rhetosDocumentFactory.CreateNew();
                    rhetosDocument.UpdateText(text);
                    rhetosDocuments[documentUri] = rhetosDocument;
                }
            }
            log.LogInformation($"Document analysis COMPLETE.");
        }*/
    }
}
