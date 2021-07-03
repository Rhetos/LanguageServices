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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class RhetosHoverHandler : IHoverHandler
    {
        private readonly ILogger<RhetosHoverHandler> log;
        private readonly Lazy<RhetosWorkspace> rhetosWorkspace;

        public RhetosHoverHandler(ILogger<RhetosHoverHandler> log, ILanguageServerFacade serverFacade)
        {
            this.log = log;
            this.rhetosWorkspace = new Lazy<RhetosWorkspace>(serverFacade.GetRequiredService<RhetosWorkspace>);
        }

        public HoverRegistrationOptions GetRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
        {
            return new HoverRegistrationOptions() {DocumentSelector = TextDocumentHandler.RhetosDocumentSelector};
        }

        // Specific empty Hover response VS2019 will handle correctly
        private static readonly Hover _emptyHoverResult = new Hover() {Contents = new MarkedStringsOrMarkupContent(new MarkupContent())};
        public Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            RhetosDocument rhetosDocument;
            // Visual Studio may issue hover before DidOpen if hover happens before solution is fully loaded
            try
            {
                rhetosDocument = rhetosWorkspace.Value.GetRhetosDocument(request.TextDocument.Uri.ToUri());
            }
            catch (InvalidOperationException)
            {
                log.LogWarning($"Trying to resolve hover on document that is not opened '{request.TextDocument.Uri}'.");
                return Task.FromResult<Hover>(_emptyHoverResult);
            }

            var descInfo = rhetosDocument.GetHoverDescriptionAtPosition(request.Position.ToLineChr());
            if (string.IsNullOrEmpty(descInfo.description))
                return Task.FromResult<Hover>(_emptyHoverResult);

            var response = new Hover()
            {
                Range = new Range(descInfo.startPosition.ToPosition(), descInfo.endPosition.ToPosition()),
                Contents = new MarkedStringsOrMarkupContent(descInfo.description)
            };
            return Task.FromResult(response);
        }
    }
}
