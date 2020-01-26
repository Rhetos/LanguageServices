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

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class HostTests
    {
        [TestMethod]
        public async Task Test1()
        {
            var loggerFactory = LoggerFactory.Create(a => a.AddNLog());
            var serverProcess = new NamedPipeServerProcess("test", loggerFactory);
            var languageClient = new LanguageClient(loggerFactory, serverProcess);
            var clientInit = languageClient.Initialize("/");

            var serverInit = Program.BuildLanguageServer(serverProcess.ClientOutputStream, serverProcess.ClientInputStream);

            Task.WaitAll(clientInit, serverInit);

            Console.WriteLine(languageClient.IsConnected.ToString());

            var textDocument = new TextDocumentItem()
            {
                Text = "ble ble ble\nblelle",
                Uri = new Uri("file://ble.rhe")
            };

            var opened = await languageClient.SendRequest<object>(DocumentNames.DidOpen, new DidOpenTextDocumentParams() {TextDocument = textDocument});

            var result = await languageClient.SendRequest<object>(DocumentNames.Completion, new CompletionParams() { TextDocument = new TextDocumentIdentifier(textDocument.Uri), Position = new Position()});

            Console.WriteLine(JsonConvert.SerializeObject(result));
            languageClient.Dispose();
        }
    }
}
