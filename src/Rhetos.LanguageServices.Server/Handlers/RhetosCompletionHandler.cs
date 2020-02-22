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

using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.LanguageServices.Server.Services;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class RhetosCompletionHandler : CompletionHandler
    {
        private readonly ILogger<RhetosCompletionHandler> log;
        private readonly RhetosWorkspace rhetosWorkspace;
        private readonly ConceptQueries conceptQueries;

        private static readonly CompletionRegistrationOptions _completionRegistrationOptions = new CompletionRegistrationOptions()
        {
            DocumentSelector = TextDocumentHandler.RhetosDocumentSelector,
            ResolveProvider = true,
            //TriggerCharacters = new Container<string>(" ")
        };

        public RhetosCompletionHandler(RhetosWorkspace rhetosWorkspace, ConceptQueries conceptQueries, ILogger<RhetosCompletionHandler> log)
            : base(_completionRegistrationOptions)
        {
            this.log = log;
            this.rhetosWorkspace = rhetosWorkspace;
            this.conceptQueries = conceptQueries;
        }

        public override bool CanResolve(CompletionItem value)
        {
            return true;
        }

        public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            var document = rhetosWorkspace.GetRhetosDocument(request.TextDocument.Uri);
            if (document == null)
                return Task.FromResult<CompletionList>(null);

            var keywords = document.GetCompletionKeywordsAtPosition(request.Position.ToLineChr());

            var completionItems = keywords
                .Select(keyword => new CompletionItem() {Label = keyword, Kind = CompletionItemKind.Keyword, Detail = conceptQueries.GetFullDescription(keyword)})
                .ToList();

            var completionList = new CompletionList(completionItems);
            log.LogInformation($"End handle completion in {sw.ElapsedMilliseconds} ms.");

            return Task.FromResult(completionList);
        }

        public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request);
        }
    }
}
