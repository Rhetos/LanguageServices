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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Rhetos.LanguageServices.LspIntegration.Test
{
    [TestClass]
    public class ServerLifecycleTests : IntegrationTestBase
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
        public void ServerStarts()
        {
            DumpLogs(ServerLogs, "Server Logs");

            Assert.IsNotNull(server.WasStarted.Result);
            AssertAnyContainsAll(ServerLogs, "Rhetos.LanguageServices.Server.RhetosLanguageServer | Initialized.");
        }

        [TestMethod]
        public void AsyncTasksStart()
        {
            DumpLogs(ServerLogs, "Server Logs");

            AssertAnyContainsAll(ServerLogs, "Starting PublishDiagnosticsRunner.PublishLoop.");
            AssertAnyContainsAll(ServerLogs, "Starting RhetosProjectMonitor.MonitorLoop.");
        }

        [TestMethod]
        public void CleanupOnShutdown()
        {
            var exited = false;
            server.Exit.Subscribe(result => exited = true);

            Cleanup();
            DumpLogs(ServerLogs, "Server Logs");

            Assert.IsTrue(exited);

            AssertAnyContainsAll(ServerLogs, "PublishLoop successfully cancelled.");
            AssertAnyContainsAll(ServerLogs, "MonitorLoop successfully cancelled.");
        }
    }
}
