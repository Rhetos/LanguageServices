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
    public class RhetosSignatureHelpHandler : SignatureHelpHandler
    {
        private static readonly SignatureHelpRegistrationOptions registrationOptions = new SignatureHelpRegistrationOptions()
        {
            DocumentSelector = TextDocumentHandler.RhetosDocumentSelector,
            TriggerCharacters = new Container<string>(".", " ")
        };
        public RhetosSignatureHelpHandler() : base(registrationOptions)
        {
        }

        public override Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            var param1 = new ParameterInformation()
            {
                Documentation = "Parameter1 documentation",
                Label = new ParameterInformationLabel("param1label")
            };

            var param2 = new ParameterInformation()
            {
                Documentation = "Parameter2 documentation",
                Label = new ParameterInformationLabel("param2label")
            };

            var signatureInformation = new SignatureInformation()
            {
                Documentation = new StringOrMarkupContent("This is signature documentation\nSecond line of documentation"),
                Label = "SignatureLabel",
                Parameters = new Container<ParameterInformation>(new [] { param1, param2})
            };

            var signatureHelp = new SignatureHelp()
            {
                ActiveParameter = 0,
                ActiveSignature = 0,
                Signatures = new Container<SignatureInformation>(new [] { signatureInformation })
            };

            return Task.FromResult(signatureHelp);
        }
    }
}
