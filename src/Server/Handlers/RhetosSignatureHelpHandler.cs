/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.Dsl;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Services;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class RhetosSignatureHelpHandler : ISignatureHelpHandler
    {
        private static readonly SignatureHelpRegistrationOptions _registrationOptions = new SignatureHelpRegistrationOptions()
        {
            DocumentSelector = TextDocumentHandler.RhetosDocumentSelector,
            TriggerCharacters = new Container<string>(".", " ")
        };

        private readonly ILogger<RhetosSignatureHelpHandler> log;
        private readonly Lazy<RhetosWorkspace> rhetosWorkspace;

        public RhetosSignatureHelpHandler(ILogger<RhetosSignatureHelpHandler> log, ILanguageServerFacade serverFacade)
        {
            this.log = log;
            this.rhetosWorkspace = new Lazy<RhetosWorkspace>(serverFacade.GetRequiredService<RhetosWorkspace>);
        }

        public SignatureHelpRegistrationOptions GetRegistrationOptions(SignatureHelpCapability capability, ClientCapabilities clientCapabilities)
        {
            return _registrationOptions;
        }

        public Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            log.LogDebug($"SignatureHelp requested at {request.Position.ToLineChr()}.");
            var rhetosDocument = rhetosWorkspace.Value.GetRhetosDocument(request.TextDocument.Uri.ToUri());
            if (rhetosDocument == null)
                return Task.FromResult<SignatureHelp>(null);

            var signatures = rhetosDocument.GetSignatureHelpAtPosition(request.Position.ToLineChr());
            if (signatures.signatures == null)
                return Task.FromResult<SignatureHelp>(null);

            ParameterInformation FromRhetosParameter(ConceptMemberSyntax conceptMember) => new ParameterInformation()
            {
                Documentation = "",
                Label = new ParameterInformationLabel(ConceptTypeTools.ConceptMemberDescription(conceptMember))
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
