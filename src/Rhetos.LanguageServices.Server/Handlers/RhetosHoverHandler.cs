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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.LanguageServices.Server.Services;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class RhetosHoverHandler : HoverHandler
    {
        private readonly RhetosWorkspace rhetosWorkspace;
        private readonly ILogger<RhetosHoverHandler> log;

        public RhetosHoverHandler(RhetosWorkspace rhetosWorkspace, ILogger<RhetosHoverHandler> log) : base(TextDocumentHandler.RhetosTextDocumentRegistrationOptions)
        {
            this.rhetosWorkspace = rhetosWorkspace;
            this.log = log;
        }

        public override Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            var rhetosDocument = rhetosWorkspace.GetRhetosDocument(request.TextDocument.Uri);
            if (rhetosDocument == null)
                return Task.FromResult<Hover>(null);

            var descInfo = rhetosDocument.GetHoverDescriptionAtPosition(request.Position.ToLineChr());
            if (string.IsNullOrEmpty(descInfo.description))
                return Task.FromResult<Hover>(null);

            var response = new Hover()
            {
                Range = new Range(descInfo.startPosition.ToPosition(), descInfo.endPosition.ToPosition()),
                Contents = new MarkedStringsOrMarkupContent(descInfo.description)
            };
            return Task.FromResult(response);
        }
    }
}
