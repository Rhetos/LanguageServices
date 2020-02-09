using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<string, RhetosDocument> rhetosDocuments = new ConcurrentDictionary<string, RhetosDocument>();
        private readonly ConcurrentDictionary<string, string> documentTextUpdates = new ConcurrentDictionary<string, string>();
        private Task analysisTask = Task.CompletedTask;
        private DateTime lastDocumentChangeTime = DateTime.MinValue;
        private readonly ILogger<RhetosWorkspace> log;
        private readonly RhetosDocumentFactory rhetosDocumentFactory;

        public RhetosWorkspace(RhetosDocumentFactory rhetosDocumentFactory, ILogger<RhetosWorkspace> log)
        {
            this.log = log;
            this.rhetosDocumentFactory = rhetosDocumentFactory;
        }

        public void UpdateDocumentText(string id, string text)
        {
            documentTextUpdates[id] = text;
            lastDocumentChangeTime = DateTime.Now;

            log.LogInformation($"Update text queued.");
            analysisTask = analysisTask.ContinueWith(_ => PendingProcessLoop());
        }

        public RhetosDocument GetRhetosDocument(string id)
        {
            if (!rhetosDocuments.TryGetValue(id, out var rhetosDocument))
                return null;

            return rhetosDocument;
        }

        public Dictionary<string, RhetosDocument> GetAllDocuments()
        {
            return rhetosDocuments.ToDictionary(document => document.Key, document => document.Value);
        }

        /*
        public List<(string documentUri, CodeAnalysisError error)> GetAllErrors()
        {
            var result = new List<(string, CodeAnalysisError)>();
            foreach (var rhetosDocument in rhetosDocuments.ToList())
            {
                var documentErrors = rhetosDocument.Value
                    .TokenizerErrors
                    .Concat(rhetosDocument.Value.CodeAnalysisErrors)
                    .Select(error => (rhetosDocument.Key, error));

                result.AddRange(documentErrors);
            }

            return result;
        }*/

        private void PendingProcessLoop()
        {
            log.LogInformation($"New ProcessLoop started.");
            while (documentTextUpdates.Any())
            {
                Task.Delay(100).Wait();
                var elapsed = DateTime.Now - lastDocumentChangeTime;
                if (elapsed < TimeSpan.FromMilliseconds(300)) continue;
                
                RunAnalysis();
            }
            log.LogInformation($"ProcessLoop completed.");
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
        }
    }
}
