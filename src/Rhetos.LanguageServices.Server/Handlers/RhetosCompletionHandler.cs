using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;

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

            var document = rhetosWorkspace.GetRhetosDocument(request.TextDocument.Uri.ToString());
            if (document == null)
                return Task.FromResult(new CompletionList());

            var keywords = document.GetCompletionKeywordsAtPosition((int) request.Position.Line, (int) request.Position.Character);

            var completionItems = keywords
                .Select(keyword => new CompletionItem() {Label = keyword, Kind = CompletionItemKind.Keyword, Detail = conceptQueries.GetDescriptionForKeyword(keyword)})
                .ToList();

            var list = new CompletionList(completionItems);

            // debug signature
            var memberDebug = document.GetAnalysis((int) request.Position.Line, (int) request.Position.Character);
            log.LogInformation($"Member info: {JsonConvert.SerializeObject(memberDebug.MemberDebug, Formatting.Indented)}");

            log.LogInformation($"End handle completion in {sw.ElapsedMilliseconds} ms.");
            return Task.FromResult(list);
        }
        
        public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request);
        }
    }
}
