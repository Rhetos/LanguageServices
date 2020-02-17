using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Rhetos.LanguageServices.Server.Services;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class TextDocumentHandler : ITextDocumentSyncHandler
    {
        public static DocumentSelector RhetosDocumentSelector = DocumentSelector.ForLanguage("rhetos-dsl");

        public static TextDocumentRegistrationOptions RhetosTextDocumentRegistrationOptions = new TextDocumentRegistrationOptions()
        {
            DocumentSelector = RhetosDocumentSelector
        };

        private readonly ILogger<TextDocumentHandler> log;
        private readonly RhetosWorkspace rhetosWorkspace;
        private readonly ServerEventHandler serverEventsHandler;

        public TextDocumentHandler(ILogger<TextDocumentHandler> log, RhetosWorkspace rhetosWorkspace, ServerEventHandler serverEventsHandler)
        {
            this.log = log;
            log.LogInformation("Initialized");
            this.rhetosWorkspace = rhetosWorkspace;
            this.serverEventsHandler = serverEventsHandler;
        }

        public TextDocumentChangeRegistrationOptions GetRegistrationOptions() =>
            new TextDocumentChangeRegistrationOptions() {DocumentSelector = RhetosDocumentSelector, SyncKind = TextDocumentSyncKind.Full};

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions() => RhetosTextDocumentRegistrationOptions;

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions() =>
            new TextDocumentSaveRegistrationOptions() {DocumentSelector = RhetosDocumentSelector, IncludeText = true};

        public void SetCapability(SynchronizationCapability capability)
        {
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            var text = notification.ContentChanges.First().Text;
            rhetosWorkspace.UpdateDocumentText(notification.TextDocument.Uri, text);
            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            var text = notification.TextDocument.Text;
            var uri = notification.TextDocument.Uri;

            log.LogInformation($"Document opened: {uri}.");
            rhetosWorkspace.UpdateDocumentText(uri, text);

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