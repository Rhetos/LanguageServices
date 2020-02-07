using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class TextDocumentHandler : ITextDocumentSyncHandler
    {
        public static DocumentSelector RhetosDocumentSelector = DocumentSelector.ForLanguage("rhetos-dsl");
        public static TextDocumentRegistrationOptions RhetosTextDocumentRegistrationOptions = new TextDocumentRegistrationOptions()
        {
            DocumentSelector = RhetosDocumentSelector
        };

        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly ILanguageServer server;
        private readonly RhetosWorkspace rhetosWorkspace;
        private readonly ServerEventHandler serverEventsHandler;
        private readonly RhetosAppContext rhetosAppContext;

        public TextDocumentHandler(ILogger<TextDocumentHandler> logger, ILanguageServer server, RhetosWorkspace rhetosWorkspace, 
            RhetosAppContext rhetosAppContext, ServerEventHandler serverEventsHandler)
        {
            _logger = logger;
            _logger.LogInformation("Initialized");
            this.server = server;
            this.rhetosWorkspace = rhetosWorkspace;
            this.serverEventsHandler = serverEventsHandler;
            this.rhetosAppContext = rhetosAppContext;
        }

        public TextDocumentChangeRegistrationOptions GetRegistrationOptions() =>
            new TextDocumentChangeRegistrationOptions() {DocumentSelector = RhetosDocumentSelector, SyncKind = TextDocumentSyncKind.Full};

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions() => RhetosTextDocumentRegistrationOptions;

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions() =>
            new TextDocumentSaveRegistrationOptions() {DocumentSelector = RhetosDocumentSelector, IncludeText = true};

        public void SetCapability(SynchronizationCapability capability)
        {
            // _logger.LogInformation(JsonConvert.SerializeObject(capability, Formatting.Indented));
        }

        // TODO: isolate lint to service to prevent overlapping linting tasks
        public Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            _logger.LogInformation($"Document changed: {notification.TextDocument.Uri}.");
            var sw = Stopwatch.StartNew();
            var text = notification.ContentChanges.First().Text;
            rhetosWorkspace.UpdateDocumentText(notification.TextDocument.Uri.ToString(), text);

            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            var text = notification.TextDocument.Text;
            var uri = notification.TextDocument.Uri;


            _logger.LogInformation($"Document opened: {uri}.");
            rhetosWorkspace.UpdateDocumentText(uri.ToString(), text);

            var pathMatch = Regex.Match(text, @"^\s*//\s*<rhetosRootPath=""(.+)""\s*/>");
            var rootPath = pathMatch.Success
                ? pathMatch.Groups[1].Value
                : null;
            serverEventsHandler.InitializeRhetosContext(rootPath);

            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            return Unit.Task;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token)
        {
            return Unit.Task;
        }
        
        public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
        {
            return new TextDocumentAttributes(uri, "rhetos-dsl");
        }
    }
}