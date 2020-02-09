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
        private readonly ConceptQueries conceptQueries;
        private readonly ILogger<RhetosHoverHandler> log;

        public RhetosHoverHandler(RhetosWorkspace rhetosWorkspace, ConceptQueries conceptQueries, ILogger<RhetosHoverHandler> log) : base(TextDocumentHandler.RhetosTextDocumentRegistrationOptions)
        {
            this.rhetosWorkspace = rhetosWorkspace;
            this.conceptQueries = conceptQueries;
            this.log = log;
        }

        public override Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            var rhetosDocument = rhetosWorkspace.GetRhetosDocument(request.TextDocument.Uri.ToString());
            if (rhetosDocument == null)
                return Task.FromResult<Hover>(null);

            var analysis = rhetosDocument.GetAnalysis((int) request.Position.Line, (int) request.Position.Character);
            if (analysis.KeywordToken == null)
                return Task.FromResult<Hover>(null);

            var description = conceptQueries.GetDescriptionForKeyword(analysis.KeywordToken.Value);
            if (string.IsNullOrEmpty(description))
                description = $"No documentation found for '{analysis.KeywordToken.Value}'.";

            description += $"\n\nToken: {analysis.KeywordToken.Value}\nNext: {analysis.NextKeywordToken?.Value}";

            var tokenStart = rhetosDocument.TextDocument.GetLineChr(analysis.KeywordToken.PositionInDslScript);
            var startPosition = new Position(tokenStart.line, tokenStart.chr);

            var endPosition = request.Position;
            if (analysis.NextKeywordToken != null)
            {
                var tokenEnd = rhetosDocument.TextDocument.GetLineChr(analysis.NextKeywordToken.PositionInDslScript - 1);
                endPosition = new Position(tokenEnd.line, tokenEnd.chr);
            }

            var response = new Hover() {Range = new Range(startPosition, endPosition), Contents = new MarkedStringsOrMarkupContent(description)};
            return Task.FromResult(response);
        }
    }
}
