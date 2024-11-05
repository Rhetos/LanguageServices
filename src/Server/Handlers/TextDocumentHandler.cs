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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.Server.Services;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class TextDocumentHandler : TextDocumentSyncHandlerBase
    {
        public static readonly TextDocumentSelector RhetosDocumentSelector = TextDocumentSelector.ForLanguage("rhetos-dsl");

        protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentSyncRegistrationOptions()
            {
                DocumentSelector = RhetosDocumentSelector,
                Change = TextDocumentSyncKind.Full,
                Save = new SaveOptions() { IncludeText = true }
            };
        }

        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new TextDocumentAttributes(uri, "csharp");

        private readonly ILogger<TextDocumentHandler> log;
        private readonly ILanguageServerFacade serverFacade;
        private readonly Lazy<RhetosWorkspace> rhetosWorkspace;

        public TextDocumentHandler(ILogger<TextDocumentHandler> log, ILanguageServerFacade serverFacade)
        {
            this.log = log;
            this.serverFacade = serverFacade;
            this.rhetosWorkspace = new Lazy<RhetosWorkspace>(serverFacade.GetRequiredService<RhetosWorkspace>);
        }

        public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            var text = request.ContentChanges.First().Text;
            rhetosWorkspace.Value.UpdateDocumentText(request.TextDocument.Uri.ToUri(), text);
            return Unit.Task;
        }

        public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            var text = request.TextDocument.Text;
            var uri = request.TextDocument.Uri.ToUri();

            log.LogTrace($"Document opened: {uri}.");
            rhetosWorkspace.Value.UpdateDocumentText(uri, text);

            return Unit.Task;
        }

        public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            log.LogDebug($"Document closed: {request.TextDocument.Uri.ToUri()}.");
            rhetosWorkspace.Value.CloseDocument(request.TextDocument.Uri.ToUri());
            return Unit.Task;
        }

        public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }
    }
}