using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NLog;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Services
{
    public class RhetosWorkspace
    {
        public DateTime LastAnalysisRunTime { get; private set; } = DateTime.MinValue;

        private readonly ConcurrentDictionary<string, RhetosDocument> rhetosDocuments = new ConcurrentDictionary<string, RhetosDocument>();
        private readonly ConcurrentDictionary<string, string> documentTextUpdates = new ConcurrentDictionary<string, string>();
        private readonly RhetosAppContext rhetosAppContext;
        private Task analysisTask = Task.CompletedTask;
        private DateTime lastDocumentChangeTime = DateTime.MinValue;
        private readonly ILogProvider rhetosLogProvider;
        private readonly ILogger<RhetosWorkspace> log;

        public RhetosWorkspace(RhetosAppContext rhetosAppContext, ILogProvider rhetosLogProvider, ILogger<RhetosWorkspace> log)
        {
            this.rhetosAppContext = rhetosAppContext;
            this.rhetosLogProvider = rhetosLogProvider;
            this.log = log;
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
            return rhetosDocuments[id];
        }

        public List<(string documentUri, CodeAnalysisError error)> GetAllErrors()
        {
            var result = new List<(string, CodeAnalysisError)>();
            foreach (var rhetosDocument in rhetosDocuments.ToList())
            {
                var documentErrors = rhetosDocument.Value
                    .TokenizerErrors
                    .Concat(rhetosDocument.Value.AnalysisErrors)
                    .Select(error => (rhetosDocument.Key, error));

                result.AddRange(documentErrors);
            }

            return result;
        }

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
                    var rhetosDocument = new RhetosDocument(rhetosAppContext, rhetosLogProvider);
                    rhetosDocument.UpdateText(text);
                    rhetosDocuments[documentUri] = rhetosDocument;
                }
            }
            LastAnalysisRunTime = DateTime.Now;
            
            log.LogInformation($"Document analysis COMPLETE.");
        }
    }
}
