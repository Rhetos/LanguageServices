using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.LanguageServices.CodeAnalysis.Services;

namespace Rhetos.LanguageServices.LspIntegration.Test
{
    [TestClass]
    [DeploymentItem("RhetosAppFolder\\", "RhetosAppFolder")]
    [DeploymentItem("RhetosAppFolder\\FolderWithApp\\MockObj\\Rhetos\\", "RhetosAppFolder\\FolderWithApp\\obj\\Rhetos")]
    [DeploymentItem("DslSyntax.json", "RhetosAppFolder\\FolderWithApp\\obj\\Rhetos\\")]
    [DeploymentItem("RhetosAppFolder\\FolderWithApp\\doc.rhe", "RhetosAppFolder\\FolderWithApp\\1\\2\\3\\")]

    public class ProjectContextTests : IntegrationTestBase
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
        public void NoRootPathIfNoDocumentsAdded()
        {
            Task.Delay(1000).Wait();

            var projectContext = server.GetRequiredService<RhetosProjectContext>();
            Assert.IsFalse(projectContext.IsInitialized);
            Assert.IsNull(projectContext.ProjectRootPath);
        }

        [TestMethod]
        public void InvalidRootPathInDirective()
        {
            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = DocumentFromText("// <rhetosProjectRootPath=\"blipblop\"/>", "RhetosAppFolder\\FolderWithApp\\doc.rhe")
            });
            Task.Delay(1000).Wait();

            DumpLogs(ServerLogs, "Server Logs");

            AssertAnyContainsAll(ServerLogs, "is not a valid Rhetos Project path");

            var projectContext = server.GetRequiredService<RhetosProjectContext>();
            Assert.IsFalse(projectContext.IsInitialized);
            Assert.IsNull(projectContext.ProjectRootPath);
        }

        [TestMethod]
        public void ValidRootPathInDirective()
        {
            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = DocumentFromText("// <rhetosProjectRootPath=\".\\\"/>", "RhetosAppFolder\\FolderWithApp\\doc.rhe")
            });
            Task.Delay(1000).Wait();

            DumpLogs(ServerLogs, "Server Logs");

            AssertAnyContainsAll(ServerLogs, "Initialized with RootPath", "RhetosAppFolder\\FolderWithApp");

            var projectContext = server.GetRequiredService<RhetosProjectContext>();
            Assert.IsTrue(projectContext.IsInitialized);
            Assert.IsTrue(projectContext.ProjectRootPath.Contains("RhetosAppFolder\\FolderWithApp"));
        }

        [TestMethod]
        public void ValidRootPathInParentFolder()
        {
            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = DocumentFromText("", "RhetosAppFolder\\FolderWithApp\\1\\2\\3\\doc.rhe")
            });
            Task.Delay(1000).Wait();

            DumpLogs(ServerLogs, "Server Logs");

            AssertAnyContainsAll(ServerLogs, "Initialized with RootPath", "RhetosAppFolder\\FolderWithApp");

            var projectContext = server.GetRequiredService<RhetosProjectContext>();
            Assert.IsTrue(projectContext.IsInitialized);
            Assert.IsTrue(projectContext.ProjectRootPath.Contains("RhetosAppFolder\\FolderWithApp"));
        }
    }
}
