using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class RhetosCompletionHandler : CompletionHandler
    {
        private readonly TrackedDocuments trackedDocuments;
        private readonly ILogger<RhetosCompletionHandler> log;
        private readonly ILanguageServer languageServer;
        private readonly RhetosAppContext rhetosAppContext;

        private static readonly CompletionRegistrationOptions _completionRegistrationOptions = new CompletionRegistrationOptions()
        {
            DocumentSelector = TextDocumentHandler.RhetosDocumentSelector,
            ResolveProvider = true,
            //TriggerCharacters = new Container<string>(" ")
        };

        public RhetosCompletionHandler(TrackedDocuments trackedDocuments, ILogger<RhetosCompletionHandler> log, 
            ILanguageServer languageServer, RhetosAppContext rhetosAppContext)
            : base(_completionRegistrationOptions)
        {
            this.trackedDocuments = trackedDocuments;
            this.log = log;
            this.languageServer = languageServer;
            this.rhetosAppContext = rhetosAppContext;
        }
        public override bool CanResolve(CompletionItem value)
        {
            return true;
        }

        public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            log.LogInformation("Start handle");
            var sw = Stopwatch.StartNew();

            var text = trackedDocuments.GetDocumentText(request.TextDocument.Uri.ToString());
            
            var rhe = new RheDocument(text, rhetosAppContext, new NLogProvider());
            var analysisResult = rhe.GetAnalysis((int) request.Position.Line, (int) request.Position.Character);
            log.LogInformation(JsonConvert.SerializeObject(analysisResult));

            var typingToken = rhe.GetTokenBeingTypedAtCursor((int) request.Position.Line, (int) request.Position.Character);
            log.LogInformation($"Typing token: '{typingToken?.Value}'.");
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
                .Select(type => ConceptInfoType.SignatureDescription(type));

            var item = new CompletionItem()
            {
                Detail = string.Join("\n", descriptions)
            };

            return Task.FromResult(item);
        }
    }
}
