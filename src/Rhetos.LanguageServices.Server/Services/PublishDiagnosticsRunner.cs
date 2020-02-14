using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private DateTime lastPublishTime = DateTime.MinValue;

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

        private void PublishLoop()
        {
            while (true)
            {
                Task.Delay(300).Wait();

                try
                {
                    LoopCycle();
                }
                catch (Exception e)
                {
                    log.LogWarning($"Error occured during document diagnostics: {e}");
                }
            }
        }

        private void LoopCycle()
        {
            var sw = Stopwatch.StartNew();

            var startPublishCheckTime = DateTime.Now;
            var updatedDocumentIds = rhetosWorkspace.DocumentChangeTimes
                .Where(a => a.Value > lastPublishTime)
                .Select(a => a.Key)
                .ToList();

            if (!updatedDocumentIds.Any())
                return;

            var publishTasks = new List<Task>();
            foreach (var documentId in updatedDocumentIds)
            {
                var rhetosDocument = rhetosWorkspace.GetRhetosDocument(documentId);
                var analysisResult = rhetosDocument.GetAnalysis();

                var diagnostics = analysisResult.AllErrors
                    .Select(error => DiagnosticFromAnalysisError(analysisResult, error));

                var publishDiagnostics = new PublishDiagnosticsParams()
                {
                    Diagnostics = new Container<Diagnostic>(diagnostics),
                    Uri = new Uri(documentId)
                };
                log.LogDebug($"Publish new diagnostics for '{documentId}'.");
                var publishTask = languageServer.SendRequest(DocumentNames.PublishDiagnostics, publishDiagnostics);
                publishTasks.Add(publishTask);
            }

            Task.WaitAll(publishTasks.ToArray());
            log.LogInformation($"Publish diagnostics complete for {publishTasks.Count} documents in {sw.Elapsed.TotalMilliseconds:0.00} ms.");
            lastPublishTime = startPublishCheckTime;
        }

        private Diagnostic DiagnosticFromAnalysisError(CodeAnalysisResult analysisResult, CodeAnalysisError error)
        {
            var start = error.LineChr.ToPosition();
            Position end;
            var tokenAtPosition = analysisResult.GetTokenAtPosition(error.LineChr);

            if (tokenAtPosition != null)
            {
                var lineChr = analysisResult.TextDocument.GetLineChr(tokenAtPosition.PositionInDslScript + tokenAtPosition.Value.Length);
                end = lineChr.ToPosition();
            }
            else
            {
                end = error.LineChr.ToPosition();
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
