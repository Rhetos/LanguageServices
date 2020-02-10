using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public class RhetosSignatureHelpHandler : SignatureHelpHandler
    {
        private static readonly SignatureHelpRegistrationOptions registrationOptions = new SignatureHelpRegistrationOptions()
        {
            DocumentSelector = TextDocumentHandler.RhetosDocumentSelector,
            TriggerCharacters = new Container<string>(".", " ", ";", "{")
        };
        private readonly RhetosWorkspace rhetosWorkspace;
        private readonly ILogger<RhetosSignatureHelpHandler> log;
        private readonly ConceptQueries conceptQueries;

        public RhetosSignatureHelpHandler(RhetosWorkspace rhetosWorkspace, ConceptQueries conceptQueries, ILogger<RhetosSignatureHelpHandler> log) : base(registrationOptions)
        {
            this.rhetosWorkspace = rhetosWorkspace;
            this.log = log;
            this.conceptQueries = conceptQueries;
        }

        public override Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            var rhetosDocument = rhetosWorkspace.GetRhetosDocument(request.TextDocument.Uri);
            if (rhetosDocument == null)
                return Task.FromResult<SignatureHelp>(null);

            // debug signature
            var analysisResult = rhetosDocument.GetAnalysis(request.Position.ToLineChr());
            //log.LogInformation($"Member info: {JsonConvert.SerializeObject(analysisResult.MemberDebug, Formatting.Indented)}");

            var keyword = analysisResult.KeywordToken?.Value;
            log.LogInformation($"Current keyword: '{keyword}' at {request.Position.ToLineChr()}.");
            log.LogInformation("\n" + rhetosDocument.TextDocument.ShowPosition(request.Position.ToLineChr()));
            var signatures = conceptQueries.GetSignaturesWithDocumentation(keyword);

            if (signatures == null)
                return Task.FromResult<SignatureHelp>(null);

            var signatureInfos = new List<SignatureInformation>();
            foreach (var signature in signatures)
            {
                var members = ConceptMembers.Get(signature.conceptInfoType);
                var parameters = members
                    .Where(member => member.IsParsable)
                    .Select(member => new ParameterInformation() {Label = new ParameterInformationLabel(member.Name)});

                var signatureInfo = new SignatureInformation()
                {
                    Documentation = new StringOrMarkupContent(signature.signatureDescription),
                    Parameters = new Container<ParameterInformation>(parameters),
                    Label = signature.conceptInfoType.Name
                };
                signatureInfos.Add(signatureInfo);
            }

            if (!signatureInfos.Any())
                return Task.FromResult<SignatureHelp>(null);

            var signatureHelp = new SignatureHelp()
            {
                ActiveParameter = 0,
                ActiveSignature = 0,
                Signatures = new Container<SignatureInformation>(signatureInfos)
            };

            return Task.FromResult(signatureHelp);
        }
    }
}
