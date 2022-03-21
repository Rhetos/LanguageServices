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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using Rhetos.LanguageServices.Server;

namespace Rhetos.LanguageServices.LspIntegration.Test
{
    [TestClass]
    [DeploymentItem("RhetosAppFolder\\", "RhetosAppFolder")]
    [DeploymentItem("RhetosAppFolder\\FolderWithApp\\MockObj\\Rhetos\\", "RhetosAppFolder\\FolderWithApp\\obj\\Rhetos")]
    [DeploymentItem("DslSyntax.json", "RhetosAppFolder\\FolderWithApp\\obj\\Rhetos\\")]
    public class BasicOperationsTests : IntegrationTestBase
    {
        private ILanguageServer server;
        private ILanguageClient client;

        [TestInitialize]
        public void Initialize()
        {
            (client, server) = Init();
        }

        [TestCleanup]
        public void Cleanup()
        {
            client.SendShutdown(new ShutdownParams());
            client.SendExit();
            server.WaitForExit.Wait(2000);
        }

        [TestMethod]
        public void Completion()
        {
            var textDocument = DocumentFromText("Module module { Entity entity {  } }\n\n", "RhetosAppFolder\\FolderWithApp\\doc.rhe");
            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = textDocument
            });
            Task.Delay(1000).Wait();

            {
                var completionResult = client.TextDocument.RequestCompletion(new CompletionParams()
                {
                    TextDocument = new TextDocumentIdentifier(textDocument.Uri),
                    Position = new Position(0, 32)
                }).AsTask().Result;

                Console.WriteLine(JsonConvert.SerializeObject(completionResult.Items.First(), Formatting.Indented));

                Console.WriteLine(completionResult.Items.Count());
                Assert.IsNotNull(completionResult);
                Assert.AreEqual(67, completionResult.Count());
            }

            {
                var completionResult = client.TextDocument.RequestCompletion(new CompletionParams()
                {
                    TextDocument = new TextDocumentIdentifier(textDocument.Uri),
                    Position = new Position(1, 0)
                }).AsTask().Result;

                Console.WriteLine(completionResult.Items.Count());
                Assert.IsNotNull(completionResult);
                Assert.AreEqual(172, completionResult.Count());
            }
        }
    }
}
