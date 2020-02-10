using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
