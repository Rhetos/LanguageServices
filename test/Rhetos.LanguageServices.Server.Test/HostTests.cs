using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NLog.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Client.Processes;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class HostTests
    {
        [TestMethod]
        public async Task Test1()
        {
            var logFactory = LoggerFactory.Create(a => a.AddNLog().SetMinimumLevel(LogLevel.Trace));
            var serverProcess = new NamedPipeServerProcess("test", logFactory);
            var languageClient = new LanguageClient(logFactory, serverProcess);
            var clientInit = languageClient.Initialize("/");

            var serverInit = Program.BuildLanguageServer(serverProcess.ClientOutputStream, serverProcess.ClientInputStream, builder => builder.AddNLog().AddLanguageServer().AddConsole());

            Task.WaitAll(clientInit, serverInit);

            Console.WriteLine(languageClient.IsConnected.ToString());

            var textDocument = new TextDocumentItem()
            {
                Text = @"// <rhetosRootPath="""" />\nble ble ble\nblelle",
                Uri = new Uri("file://ble.rhe")
            };

            var opened = await languageClient.SendRequest<object>(DocumentNames.DidOpen, new DidOpenTextDocumentParams() {TextDocument = textDocument});

            Task.Delay(2500).Wait();

            var result = await languageClient.SendRequest<CompletionList>(DocumentNames.Completion, new CompletionParams() { TextDocument = new TextDocumentIdentifier(textDocument.Uri), Position = new Position()});
            Console.WriteLine(JsonConvert.SerializeObject(result.Items, Formatting.Indented));

            Task.Delay(3000).Wait();
            languageClient.Dispose();
        }
    }
}
