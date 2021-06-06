using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using Rhetos.LanguageServices.Server;

namespace Rhetos.LanguageServices.LspIntegration.Test
{
    public class IntegrationTestBase : LanguageProtocolTestBase
    {
        public IEnumerable<string> ServerLogs => ServerLogProvider.Logs;
        public IEnumerable<string> ClientLogs => logEntries;
        public IEnumerable<string> ClientAsyncLogs => asyncLogEntries;
        public IEnumerable<string> Diagnostics => DiagnosticsToMessages();

        protected MemoryLoggerProvider ServerLogProvider { get; } = new();
        private readonly List<string> logEntries = new();
        private readonly List<string> asyncLogEntries = new();
        private readonly Dictionary<string, List<Diagnostic>> documentDiagnostics = new();

        public IntegrationTestBase() : base(new JsonRpcTestOptions())
        {
        }

        protected (ILanguageClient client, ILanguageServer server) Init()
        {
            var (client, server) = Initialize(
                clientOptions =>
                {
                    clientOptions.OnLogMessage(OnLogMessage);
                    clientOptions.OnPublishDiagnostics(OnPublishDiagnostics);
                },
                serverOptions =>
                {
                    RhetosLanguageServer.ConfigureLanguageServer(serverOptions);
                    serverOptions.ConfigureLogging(b =>
                    {
                        b.AddProvider(ServerLogProvider);
                        b.AddLanguageProtocolLogging();
                        RhetosLanguageServer.ConfigureLoggingExplicitFilters(b, LogLevel.Warning, LogLevel.Trace);
                    });
                })
                .Result;

            RhetosLanguageServer.AddServerCleanupHandlers(server);

            return (client, server);
        }

        public TextDocumentItem DocumentFromText(string text, string relativeUri, string baseUri = null)
        {
            var textDocument = new TextDocumentItem()
            {
                LanguageId = "rhetos-dsl",
                Uri = DocumentUri.From(Path.Combine(baseUri ?? Environment.CurrentDirectory, relativeUri)),
                Text = text
            };
            return textDocument;
        }

        public int CountContainsAll(IEnumerable<string> collection, params string[] subStrings)
        {
            return collection
                .Count(entry => subStrings.All(sub => entry.Contains(sub)));
        }

        public void AssertAnyContainsAll(IEnumerable<string> collection, params string[] subStrings)
        {
            var count = CountContainsAll(collection, subStrings);
            if (count == 0)
                throw new AssertFailedException("Expected at least one occurrence of provided substrings, but found none.");
        }

        public void DumpLogs(IEnumerable<string> entries, string title = null)
        {
            if (!string.IsNullOrEmpty(title))
                Console.WriteLine($"\n{title}:\n=======================================");

            foreach (var logEntry in entries)
                Console.WriteLine(logEntry);
            Console.WriteLine(">> Log dump END\n");
        }

        private IEnumerable<string> DiagnosticsToMessages()
        {
            return documentDiagnostics
                .SelectMany(entry => entry.Value.Select(diag => FormatDiagnosticMessage(entry.Key, diag.Message, diag.Range.ToString())))
                .ToList();
        }

        private void OnLogMessage(LogMessageParams msgParams)
        {
            var formattedMessage = $"[{msgParams.Type}] {msgParams.Message}";
            Console.WriteLine(formattedMessage);

            if (msgParams.Message.StartsWith("Rhetos.LanguageServices.Server.Services.PublishDiagnosticsRunner")
                || msgParams.Message.StartsWith("Rhetos.LanguageServices.Server.Services.RhetosProjectMonitor"))
            {
                asyncLogEntries.Add(formattedMessage);
            }
            else
            {
                logEntries.Add(formattedMessage);
            }
        }

        private void OnPublishDiagnostics(PublishDiagnosticsParams diagnosticParams)
        {
            documentDiagnostics[diagnosticParams.Uri.ToUri().AbsolutePath] = diagnosticParams.Diagnostics.ToList();
        }

        private string FormatDiagnosticMessage(string documentUri, string message, string position)
        {
            return $"{message} in '{documentUri}' at {position}";
        }
    }
}
