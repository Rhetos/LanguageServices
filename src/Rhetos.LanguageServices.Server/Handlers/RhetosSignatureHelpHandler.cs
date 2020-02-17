using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class RhetosSignatureHelpHandler : SignatureHelpHandler
    {
        private static readonly SignatureHelpRegistrationOptions _registrationOptions = new SignatureHelpRegistrationOptions()
        {
            DocumentSelector = TextDocumentHandler.RhetosDocumentSelector,
            TriggerCharacters = new Container<string>(".", " ", ";", "{")
        };

        private readonly RhetosWorkspace rhetosWorkspace;
        private readonly ILogger<RhetosSignatureHelpHandler> log;

        public RhetosSignatureHelpHandler(RhetosWorkspace rhetosWorkspace, ILogger<RhetosSignatureHelpHandler> log) : base(_registrationOptions)
        {
            this.rhetosWorkspace = rhetosWorkspace;
            this.log = log;
        }

        public override Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            log.LogDebug($"SignatureHelp requested at {request.Position.ToLineChr()}.");
            var rhetosDocument = rhetosWorkspace.GetRhetosDocument(request.TextDocument.Uri);
            if (rhetosDocument == null)
                return Task.FromResult<SignatureHelp>(null);

            var signatures = rhetosDocument.GetSignatureHelpAtPosition(request.Position.ToLineChr());
            if (signatures.signatures == null)
                return Task.FromResult<SignatureHelp>(null);

            ParameterInformation FromRhetosParameter(ConceptMember conceptMember) => new ParameterInformation()
            {
                Documentation = "",
                Label = new ParameterInformationLabel(ConceptInfoType.ConceptMemberDescription(conceptMember))
            };

            SignatureInformation FromRhetosSignature(RhetosSignature rhetosSignature) => new SignatureInformation()
            {
                Documentation = rhetosSignature.Documentation,
                Label = rhetosSignature.Signature,
                Parameters = new Container<ParameterInformation>(rhetosSignature.Parameters.Select(FromRhetosParameter))
            };

            var signatureHelp = new SignatureHelp()
            {
                Signatures = new Container<SignatureInformation>(signatures.signatures.Select(FromRhetosSignature)),
                ActiveSignature = signatures.activeSignature ?? 100,
                ActiveParameter = signatures.activeParameter ?? 100
            };

            return Task.FromResult(signatureHelp);
        }
    }
}
