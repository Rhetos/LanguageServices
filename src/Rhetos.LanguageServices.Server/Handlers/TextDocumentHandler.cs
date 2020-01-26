using System;
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

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class TextDocumentHandler : ITextDocumentSyncHandler
    {
        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly DocumentSelector _documentSelector;
        
        public TextDocumentHandler(ILogger<TextDocumentHandler> logger)
        {
            _logger = logger;
            _logger.LogInformation("Initialized");
            _documentSelector = DocumentSelector.ForLanguage("rhetos-dsl");
        }

        public TextDocumentChangeRegistrationOptions GetRegistrationOptions() =>
            new TextDocumentChangeRegistrationOptions() {DocumentSelector = _documentSelector, SyncKind = TextDocumentSyncKind.Full};

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions() =>
            new TextDocumentRegistrationOptions() {DocumentSelector = _documentSelector};

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions() =>
            new TextDocumentSaveRegistrationOptions() {DocumentSelector = _documentSelector, IncludeText = true};

        public void SetCapability(SynchronizationCapability capability)
        {
            _logger.LogInformation(JsonConvert.SerializeObject(capability, Formatting.Indented));
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            _logger.LogInformation("Document changed!");
            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            _logger.LogInformation("Document opened!");
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
            _logger.LogInformation($"Setting document attributes for '{uri}'.");
            return new TextDocumentAttributes(uri, "rhetos-dsl");
        }
    }
}