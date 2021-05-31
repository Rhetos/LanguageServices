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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Rhetos.LanguageServices.CodeAnalysis.Services;
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
            rhetosWorkspace.UpdateDocumentText(notification.TextDocument.Uri.ToUri(), text);
            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            var text = notification.TextDocument.Text;
            var uri = notification.TextDocument.Uri.ToUri();

            log.LogDebug($"Document opened: {uri}.");
            rhetosWorkspace.UpdateDocumentText(uri, text);

            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            log.LogDebug($"Document closed: {notification.TextDocument.Uri.ToUri()}.");
            rhetosWorkspace.CloseDocument(notification.TextDocument.Uri.ToUri());
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