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
