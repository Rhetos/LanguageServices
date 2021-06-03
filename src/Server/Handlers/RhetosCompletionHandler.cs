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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.Server.Services;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class RhetosCompletionHandler : ICompletionHandler
    {
        private readonly ILogger<RhetosCompletionHandler> log;
        private readonly Lazy<RhetosWorkspace> rhetosWorkspace;
        private readonly Lazy<ConceptQueries> conceptQueries;

        private static readonly CompletionRegistrationOptions _completionRegistrationOptions = new CompletionRegistrationOptions()
        {
            DocumentSelector = TextDocumentHandler.RhetosDocumentSelector,
            ResolveProvider = true,
            TriggerCharacters = new Container<string>("."),
            AllCommitCharacters = new Container<string>(" ", ".")
        };

        public RhetosCompletionHandler(ILogger<RhetosCompletionHandler> log, ILanguageServerFacade serverFacade)
        {
            this.log = log;
            this.rhetosWorkspace = new Lazy<RhetosWorkspace>(serverFacade.GetRequiredService<RhetosWorkspace>);
            this.conceptQueries = new Lazy<ConceptQueries>(serverFacade.GetRequiredService<ConceptQueries>);
        }

        public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            return _completionRegistrationOptions;
        }

        public bool CanResolve(CompletionItem value)
        {
            return true;
        }

        public Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            log.LogDebug($"Completion requested at {request.Position.ToLineChr()}.");

            var document = rhetosWorkspace.Value.GetRhetosDocument(request.TextDocument.Uri.ToUri());
            if (document == null)
                return Task.FromResult<CompletionList>(null);

            var keywords = document.GetCompletionKeywordsAtPosition(request.Position.ToLineChr());

            var completionItems = keywords
                .Select(keyword => new CompletionItem() {Label = keyword, Kind = CompletionItemKind.Keyword, Detail = conceptQueries.Value.GetFullDescription(keyword)})
                .ToList();

            var completionList = new CompletionList(completionItems);

            return Task.FromResult(completionList);
        }

        public Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request);
        }
    }
}
