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
        private readonly RhetosAppContext rhetosAppContext;
        private readonly XmlDocumentationProvider xmlDocumentationProvider;
        private readonly RhetosWorkspace rhetosWorkspace;

        private static readonly CompletionRegistrationOptions _completionRegistrationOptions = new CompletionRegistrationOptions()
        {
            DocumentSelector = TextDocumentHandler.RhetosDocumentSelector,
            ResolveProvider = true,
            //TriggerCharacters = new Container<string>(" ")
        };

        public RhetosCompletionHandler(RhetosWorkspace rhetosWorkspace, ILogger<RhetosCompletionHandler> log, 
            RhetosAppContext rhetosAppContext, XmlDocumentationProvider xmlDocumentationProvider)
            : base(_completionRegistrationOptions)
        {
            this.log = log;
            this.rhetosAppContext = rhetosAppContext;
            this.xmlDocumentationProvider = xmlDocumentationProvider;
            this.rhetosWorkspace = rhetosWorkspace;
        }
        public override bool CanResolve(CompletionItem value)
        {
            return true;
        }

        public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();

            var document = rhetosWorkspace.GetRhetosDocument(request.TextDocument.Uri.ToString());
            var analysisResult = document.GetAnalysis((int) request.Position.Line, (int) request.Position.Character);
            //log.LogInformation(JsonConvert.SerializeObject(analysisResult));

            var typingToken = document.GetTokenBeingTypedAtCursor((int) request.Position.Line, (int) request.Position.Character);
            //log.LogInformation($"Typing token: '{typingToken?.Value}'.");
            if (analysisResult.KeywordToken != null && analysisResult.KeywordToken != typingToken)
                return Task.FromResult(new CompletionList());
            
            var conceptQueries = new ConceptQueries(rhetosAppContext);

            var lastParent = analysisResult.ConceptContext.LastOrDefault();
            var validConcepts = lastParent == null
                ? rhetosAppContext.ConceptInfoTypes.ToList()
                : conceptQueries.ValidConceptsForParent(lastParent.GetType());

            var keywords = validConcepts
                .Select(concept => ConceptInfoHelper.GetKeyword(concept))
                .Where(keyword => keyword != null)
                .Distinct()
                .ToList();

            var completionItems = keywords
                .Select(keyword => new CompletionItem()
                {
                    Label = keyword,
                    Kind = CompletionItemKind.Keyword
                })
                .ToList();

            var list = new CompletionList(completionItems);
            
            log.LogInformation($"End handle completion in {sw.ElapsedMilliseconds} ms.");
            return Task.FromResult(list);
        }
        
        public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            if (!rhetosAppContext.Keywords.ContainsKey(request.Label))
                return Task.FromResult(new CompletionItem());
            var keywordTypes = rhetosAppContext.Keywords[request.Label];

            var descriptions = keywordTypes
                .Select(type =>
                {
                    var signature = ConceptInfoType.SignatureDescription(type);
                    var documentation = xmlDocumentationProvider.GetDocumentation(type);
                    if (!string.IsNullOrEmpty(documentation)) documentation = $"\n* {documentation}\n";
                    return signature + documentation;
                });

            var allDescriptions = string.Join("\n", descriptions);
            var item = new CompletionItem()
            {
                Detail = allDescriptions
            };

            return Task.FromResult(item);
        }
    }
}
