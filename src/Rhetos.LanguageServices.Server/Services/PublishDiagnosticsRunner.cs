using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;

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
            var lastPublishTime = DateTime.Now;
            while (true)
            {
                Task.Delay(100).Wait();

                if (rhetosWorkspace.LastAnalysisRunTime > lastPublishTime)
                {
                    var publishTasks = new List<Task>();
                    var allErrors = rhetosWorkspace.GetAllErrors();
                    var errorsByDocument = allErrors.GroupBy(error => error.documentUri);
                    foreach (var documentErrors in errorsByDocument)
                    {
                        var diagnostics = documentErrors
                            .Select(error => new Diagnostic()
                            {
                                Severity = DiagnosticSeverity.Error,
                                Message = error.error.Message,
                                Range = new Range(new Position(error.error.Line, error.error.Chr), new Position(error.error.Line, error.error.Chr)),
                            });

                        var publishDiagnostics = new PublishDiagnosticsParams()
                        {
                            Diagnostics = new Container<Diagnostic>(diagnostics),
                            Uri = new Uri(documentErrors.Key)
                        };
                        log.LogInformation($"Publish new diagnostics for '{documentErrors.Key}'.");
                        var publishTask = languageServer.SendRequest(DocumentNames.PublishDiagnostics, publishDiagnostics);
                        publishTasks.Add(publishTask);
                    }

                    Task.WaitAll(publishTasks.ToArray());
                    log.LogInformation($"Publish diagnostics complete for {publishTasks.Count} documents.");
                    lastPublishTime = rhetosWorkspace.LastAnalysisRunTime;
                }
            }
        }
    }
}
