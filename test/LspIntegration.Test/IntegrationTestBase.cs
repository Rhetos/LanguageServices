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
