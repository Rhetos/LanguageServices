using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class RhetosHoverHandler : HoverHandler
    {
        public RhetosHoverHandler() : base(TextDocumentHandler.RhetosTextDocumentRegistrationOptions)
        {

        }

        public override Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            var response = new Hover() {Range = new Range(request.Position, request.Position), Contents = new MarkedStringsOrMarkupContent("Hover help ble ble")};
            return Task.FromResult(response);
        }
    }
}
