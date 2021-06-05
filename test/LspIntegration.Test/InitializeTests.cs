using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using Rhetos.LanguageServices.Server;

namespace Rhetos.LanguageServices.LspIntegration.Test
{
    [TestClass]
    public class InitializeTests : LanguageProtocolTestBase
    {
        public InitializeTests() : base(new JsonRpcTestOptions()) { }

        [TestMethod]
        public async Task Test()
        {
            var (client, server) = await Initialize(
                clientOptions =>
                {
                    clientOptions.OnLogMessage(msg => Console.WriteLine(msg.Message));
                    // clientOptions.OnLogTrace(msg => Console.WriteLine(msg.Message));
                },
                serverOptions =>
                {
                    RhetosLanguageServer.ConfigureLanguageServer(serverOptions);
                    serverOptions.ConfigureLogging(b =>
                    {
                        b.AddLanguageProtocolLogging();
                        RhetosLanguageServer.ConfigureLoggingExplicitFilters(b, LogLevel.Warning, LogLevel.Trace);
                    });
                });

            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = new TextDocumentItem()
                {
                    LanguageId = "rhetos-dsl", 
                    Uri = DocumentUri.From("c:\\ble.rhe"),
                    Text = "sdlkjsdjfksdkjf"
                }
            });

            await Task.Delay(5000);


            //await SettleNext();

            /*
            var completion = await client.RequestCompletion(new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(DocumentUri.From("c:\\ble.rhe")),
                Position = new Position(0, 5)
                
            });
            */
            //await client.Initialize(new CancellationToken());
            //var items = await client.RequestCompletion(new CompletionParams());
            //Console.WriteLine(items.Items.Count());

        }

        [TestMethod]
        public void Ble()
        {

        }
    }
}
