using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Rhetos.LanguageServices.Server.Parsing;

namespace Rhetos.LanguageServices.Server.Services
{
    public class PublishDiagnosticsRunner
    {
        private readonly RhetosWorkspace rhetosWorkspace;
        private readonly ILanguageServer languageServer;
        private readonly ILogger<PublishDiagnosticsRunner> log;

        public PublishDiagnosticsRunner(RhetosWorkspace rhetosWorkspace, ILanguageServer languageServer, ILogger<PublishDiagnosticsRunner> log)
        {
            this.rhetosWorkspace = rhetosWorkspace;
            this.languageServer = languageServer;
            this.log = log;
        }

        public void Start()
        {
            log.LogInformation($"Starting {nameof(PublishDiagnosticsRunner)}.");
            Task.Factory.StartNew(PublishLoop, TaskCreationOptions.LongRunning);
        }

        // TODO: publish only documents with changed analysis
        // TODO: publish documents with no errors!
        private void PublishLoop()
        {
            var lastPublishTime = DateTime.MinValue;

            while (true)
            {
                Task.Delay(100).Wait();

                var startPublishCheckTime = DateTime.Now;
                var updatedDocuments = rhetosWorkspace.GetAllDocuments()
                    .Where(document => document.Value.LastCodeAnalysisRun > lastPublishTime)
                    .ToList();

                if (!updatedDocuments.Any()) 
                    continue;

                var publishTasks = new List<Task>();
                foreach (var updatedDocument in updatedDocuments)
                {
                    var diagnostics = updatedDocument.Value.AllAnalysisErrors
                        .Select(error => DiagnosticFromAnalysisError(updatedDocument.Value, error));

                    var publishDiagnostics = new PublishDiagnosticsParams()
                    {
                        Diagnostics = new Container<Diagnostic>(diagnostics),
                        Uri = new Uri(updatedDocument.Key)
                    };
                    log.LogInformation($"Publish new diagnostics for '{updatedDocument.Key}'.");
                    var publishTask = languageServer.SendRequest(DocumentNames.PublishDiagnostics, publishDiagnostics);
                    publishTasks.Add(publishTask);
                }

                Task.WaitAll(publishTasks.ToArray());
                log.LogInformation($"Publish diagnostics complete for {publishTasks.Count} documents.");
                lastPublishTime = startPublishCheckTime;
            }
        }

        private Diagnostic DiagnosticFromAnalysisError(RhetosDocument rhetosDocument, CodeAnalysisError error)
        {
            var start = new Position(error.Line, error.Chr);
            Position end;
            var tokenAtPosition = rhetosDocument.GetTokenAtPosition(error.Line, error.Chr);

            if (tokenAtPosition != null)
            {
                var (line, chr) = rhetosDocument.TextDocument.GetLineChr(tokenAtPosition.PositionInDslScript + tokenAtPosition.Value.Length);
                end = new Position(line, chr);
            }
            else
            {
                end = new Position(error.Line, error.Chr);
            }

            return new Diagnostic()
            {
                Severity = DiagnosticSeverity.Error,
                Message = error.Message,
                Range = new Range(start, end),
            };
        }
    }
}
