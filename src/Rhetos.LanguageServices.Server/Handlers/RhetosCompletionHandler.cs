using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class RhetosCompletionHandler : CompletionHandler
    {
        public RhetosCompletionHandler() 
            : base(new CompletionRegistrationOptions()
            {
                DocumentSelector = new DocumentSelector(new DocumentFilter() {Pattern = "**/*.rhe"}),
            })
        {

        }
        public override bool CanResolve(CompletionItem value)
        {
            return true;
        }

        public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var item = new CompletionItem() { Detail = "Ovo je detail", Label = "Ovo je label"};
            var list = new CompletionList(new[] {item});

            return Task.FromResult(list);
        }
        
        public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            var item = new CompletionItem() { Detail = "Delayed request", Label = "delayed label" };
            return Task.FromResult(item);
        }
    }
}
