using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
        private readonly RhetosAppContext rhetosAppContext;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Task publishLoopTask;

        public PublishDiagnosticsRunner(RhetosWorkspace rhetosWorkspace, RhetosAppContext rhetosAppContext, ILanguageServer languageServer, ILogger<PublishDiagnosticsRunner> log)
        {
            this.rhetosWorkspace = rhetosWorkspace;
            this.languageServer = languageServer;
            this.log = log;
            this.rhetosAppContext = rhetosAppContext;

        }

        public void Start()
        {
            if (cancellationTokenSource.IsCancellationRequested)
                return;

            log.LogInformation($"Starting {nameof(PublishDiagnosticsRunner)}.");
            publishLoopTask = Task.Factory.StartNew(() => PublishLoop(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            try
            {
                log.LogDebug($"Stopping {nameof(PublishLoop)}.");
                cancellationTokenSource.Cancel();
                publishLoopTask?.Wait();
            }
            catch (Exception e)
            {
                if (e is AggregateException aggregateException && aggregateException.InnerExceptions.Any(inner => !(inner is TaskCanceledException)))
                    log.LogDebug($"{nameof(PublishLoop)} successfully cancelled.");
                else
                    log.LogDebug($"{nameof(PublishLoop)} faulted while waiting to cancel: {publishLoopTask?.Exception}");
            }
        }

        private void PublishLoop(CancellationToken cancellationToken)
        {
            while (true)
            {
                Task.Delay(300, cancellationToken).Wait(cancellationToken);

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
            if (!rhetosAppContext.IsInitialized) 
                return;

            var sw = Stopwatch.StartNew();

            var startPublishCheckTime = DateTime.Now;
            var updatedDocumentIds = rhetosWorkspace.DocumentChangeTimes
                .Where(changed => changed.Value > lastPublishTime)
                .Select(changed => changed.Key)
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
