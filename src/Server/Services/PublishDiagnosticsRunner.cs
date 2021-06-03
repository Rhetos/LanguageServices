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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.LayoutRenderers;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Rhetos.LanguageServices.Server.Services
{
    public class PublishDiagnosticsRunner
    {
        private static readonly TimeSpan _cycleInterval = TimeSpan.FromMilliseconds(300);

        private readonly Lazy<RhetosWorkspace> rhetosWorkspace;
        private readonly ILanguageServerFacade languageServerFacade;
        private readonly ILogger<PublishDiagnosticsRunner> log;

        private DateTime lastPublishTime = DateTime.MinValue;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Task publishLoopTask;

        public PublishDiagnosticsRunner(ILanguageServerFacade languageServerFacade, ILogger<PublishDiagnosticsRunner> log)
        {
            this.rhetosWorkspace = new Lazy<RhetosWorkspace>(languageServerFacade.GetRequiredService<RhetosWorkspace>);
            this.languageServerFacade = languageServerFacade;
            this.log = log;
        }

        public void Start()
        {
            if (cancellationTokenSource.IsCancellationRequested)
                return;

            log.LogInformation($"Starting {nameof(PublishDiagnosticsRunner)}.{nameof(PublishLoop)}.");
            publishLoopTask = Task.Factory.StartNew(() => PublishLoop(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            try
            {
                log.LogDebug($"Stopping {nameof(PublishDiagnosticsRunner)}.{nameof(PublishLoop)}.");
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
                Task.Delay(_cycleInterval, cancellationToken).Wait(cancellationToken);

                try
                {
                    LoopCycle();
                }
                catch (Exception e)
                {
                    log.LogWarning($"Error occurred during document diagnostics: {e}");
                }
            }
        }

        private void LoopCycle()
        {
            var sw = Stopwatch.StartNew();
            var startPublishCheckTime = DateTime.Now;
            var publishTasks = new List<Task>();

            var publishDiagnosticsChanged = rhetosWorkspace.Value.GetUpdatedDocuments(lastPublishTime)
                .Select(DiagnosticParamsFromRhetosDocument);

            var publishDiagnosticsRemoved = rhetosWorkspace.Value.GetClosedDocuments(lastPublishTime)
                .Select(documentUri => new PublishDiagnosticsParams() {Uri = documentUri, Diagnostics = new Container<Diagnostic>()});

            var allPublishDiagnostics = publishDiagnosticsChanged
                .Concat(publishDiagnosticsRemoved)
                .ToList();

            if (!allPublishDiagnostics.Any())
                return;

            foreach (var diagnostics in allPublishDiagnostics)
            {
                log.LogTrace($"Publish new diagnostics for '{diagnostics.Uri}'.");
                var publishTask = new Task(() => languageServerFacade.SendNotification(diagnostics));
                publishTasks.Add(publishTask);
            }

            Task.WaitAll(publishTasks.ToArray());
            log.LogDebug($"Publish diagnostics complete for {publishTasks.Count} documents in {sw.Elapsed.TotalMilliseconds:0.00} ms.");
            lastPublishTime = startPublishCheckTime;
        }

        private PublishDiagnosticsParams DiagnosticParamsFromRhetosDocument(Uri documentUri)
        {
            var rhetosDocument = rhetosWorkspace.Value.GetRhetosDocument(documentUri);
            var analysisResult = rhetosDocument.GetAnalysis();

            var diagnostics = analysisResult.AllErrors
                .Select(error => DiagnosticFromAnalysisError(analysisResult, error));

            return new PublishDiagnosticsParams()
            {
                Diagnostics = new Container<Diagnostic>(diagnostics),
                Uri = documentUri
            };
        }

        private Diagnostic DiagnosticFromAnalysisError(CodeAnalysisResult analysisResult, CodeAnalysisError error)
        {
            var start = error.LineChr.ToPosition();
            Position end;
            var tokenAtPosition = analysisResult.GetTokenAtPosition(error.LineChr);

            if (tokenAtPosition != null)
            {
                var lineChr = analysisResult.TextDocument.GetLineChr(tokenAtPosition.PositionEndInDslScript);
                end = lineChr.ToPosition();
            }
            else
            {
                end = error.LineChr.ToPosition();
            }

            return new Diagnostic()
            {
                Severity = error.Severity == CodeAnalysisError.ErrorSeverity.Error ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                Message = error.Message,
                Range = new Range(start, end),
            };
        }
    }
}
