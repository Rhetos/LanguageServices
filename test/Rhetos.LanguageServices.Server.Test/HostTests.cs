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
        [Ignore("Needs configuration")]
        public async Task Test1()
        {
            var logFactory = LoggerFactory.Create(a => a.AddNLog().SetMinimumLevel(LogLevel.Trace));
            var serverProcess = new NamedPipeServerProcess("test", logFactory);
            var languageClient = new LanguageClient(logFactory, serverProcess);
            var clientInit = languageClient.Initialize("/");

            var serverInit = RhetosLanguageServer.BuildLanguageServer(serverProcess.ClientOutputStream, serverProcess.ClientInputStream, builder => builder.AddNLog().AddLanguageServer().AddConsole());

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
