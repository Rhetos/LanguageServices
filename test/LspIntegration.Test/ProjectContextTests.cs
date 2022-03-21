using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    [DeploymentItem("RhetosAppFolder\\FolderWithApp2\\MockObj\\Rhetos\\", "RhetosAppFolder\\FolderWithApp2\\obj\\Rhetos")]
    [DeploymentItem("DslSyntax.json", "RhetosAppFolder\\FolderWithApp2\\obj\\Rhetos\\")]
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
        public void WillReinitializeContextOnDslSyntaxModified()
        {
            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = DocumentFromText("", "RhetosAppFolder\\FolderWithApp2\\doc.rhe")
            });
            Task.Delay(1500).Wait();

            var projectContext = server.GetRequiredService<RhetosProjectContext>();
            Assert.IsTrue(projectContext.IsInitialized);
            Assert.IsTrue(projectContext.ProjectRootPath?.Contains("FolderWithApp2"));

            var dslSyntaxFile = Path.Combine(Environment.CurrentDirectory, "RhetosAppFolder\\FolderWithApp2\\obj\\Rhetos", "DslSyntax.json");
            Assert.IsTrue(File.Exists(dslSyntaxFile));
            Assert.AreEqual(1, CountContainsAll(ServerLogs, "Initialized with RootPath", "FolderWithApp2"));
            
            File.SetLastWriteTime(dslSyntaxFile, DateTime.Now);
            Console.WriteLine($"Updating last modified of '{dslSyntaxFile}'.");
            Task.Delay(1500).Wait();
            Assert.AreEqual(2, CountContainsAll(ServerLogs, "Initialized with RootPath", "FolderWithApp2"));

            DumpLogs(ServerLogs, "Server Logs");
        }

        [TestMethod]
        public void WillSwitchOnUnusedProjectPath()
        {
            var initialDocument = DocumentFromText("", "RhetosAppFolder\\FolderWithApp\\doc.rhe");
            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = initialDocument
            });
            Task.Delay(1500).Wait();

            var projectContext = server.GetRequiredService<RhetosProjectContext>();
            Assert.IsTrue(projectContext.IsInitialized);
            Assert.IsTrue(projectContext.ProjectRootPath?.Contains("FolderWithApp"));

            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = DocumentFromText("", "RhetosAppFolder\\FolderWithApp2\\doc.rhe")
            });
            Task.Delay(1500).Wait();

            Assert.IsTrue(projectContext.IsInitialized);
            Assert.IsFalse(projectContext.ProjectRootPath?.Contains("FolderWithApp2"));

            client.TextDocument.DidCloseTextDocument(new DidCloseTextDocumentParams()
            {
                TextDocument = initialDocument.Uri
            });
            Task.Delay(1500).Wait();
            Assert.IsTrue(projectContext.IsInitialized);
            Assert.IsTrue(projectContext.ProjectRootPath?.Contains("FolderWithApp2"));

            DumpLogs(ServerLogs, "Server Logs");
        }

        [TestMethod]
        public void WillInitializeOnAssetsFolderNewlyCreated()
        {
            var projectFolder = Path.Combine(Environment.CurrentDirectory, "RhetosAppFolder\\RhetosAppDynamic");
            Assert.IsTrue(Directory.Exists(projectFolder));

            var objFolder = Path.Combine(projectFolder, "obj");
            
            if (Directory.Exists(objFolder))
                Directory.Delete(objFolder, true);
            
            var assetsFolder = Path.Combine(objFolder, "Rhetos");
            Assert.IsFalse(Directory.Exists(assetsFolder));

            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = DocumentFromText("", Path.Combine(projectFolder, "doc.rhe"))
            });
            Task.Delay(1500).Wait();

            var projectContext = server.GetRequiredService<RhetosProjectContext>();
            Assert.IsNull(projectContext.ProjectRootPath);

            Directory.CreateDirectory(assetsFolder);
            var dslSyntaxFilePath = Path.Combine(assetsFolder, "DslSyntax.json");
            File.WriteAllText(dslSyntaxFilePath, "{ \"ConceptTypes\": [] }");

            Task.Delay(1500).Wait();
            Assert.IsTrue(projectFolder.Equals(projectContext.ProjectRootPath, StringComparison.InvariantCultureIgnoreCase));
            
            DumpLogs(ServerLogs, "Server Logs");
        }

        [TestMethod]
        public void NoRootPathIfNoDocumentsAdded()
        {
            Task.Delay(1500).Wait();

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
            Task.Delay(1500).Wait();

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
            Task.Delay(1500).Wait();

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
            Task.Delay(1500).Wait();

            DumpLogs(ServerLogs, "Server Logs");

            AssertAnyContainsAll(ServerLogs, "Initialized with RootPath", "RhetosAppFolder\\FolderWithApp");

            var projectContext = server.GetRequiredService<RhetosProjectContext>();
            Assert.IsTrue(projectContext.IsInitialized);
            Assert.IsTrue(projectContext.ProjectRootPath.Contains("RhetosAppFolder\\FolderWithApp"));
        }

        [TestMethod]
        public void UnsupportedDslSyntaxVersion()
        {
            var textDocumentItem = DocumentFromText("Entity e;", "RhetosAppFolder\\FolderWithAppUnsupported\\doc.rhe");
            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams()
            {
                TextDocument = textDocumentItem
            });

            Task.Delay(1500).Wait();
            DumpLogs(ServerLogs, "Server Logs");
            Assert.AreEqual(1, CountContainsAll(ServerLogs, "Warning", "The application uses a newer version of the DSL syntax", "6.0", "5.0"));

            // Wait again to test that the failed context initialization is not executed repeatedly, since the root path and DSL syntax modification time are unchanged.
            Task.Delay(1500).Wait();
            Assert.AreEqual(1, CountContainsAll(ServerLogs, "Warning", "The application uses a newer version of the DSL syntax", "6.0", "5.0"));

            var projectContext = server.GetRequiredService<RhetosProjectContext>();
            Assert.IsTrue(projectContext.IsInitialized);
            Assert.IsTrue(projectContext.InitializationError.Message.Contains("Please install the latest version of Rhetos Language Services"));

            Console.WriteLine("Diagnostics:" + string.Concat(Diagnostics.Select((d, x) => $"{Environment.NewLine}{x + 1}: {d}")));
            Assert.IsTrue(Diagnostics.Single().StartsWith("Please install the latest version of Rhetos Language Services. The project uses a newer version of the DSL syntax: DSL version 6.0, Rhetos . Currently installed Rhetos Language Services supports DSL version 5.0."));

            var completionResult = client.TextDocument.RequestCompletion(new CompletionParams()
            {
                TextDocument = textDocumentItem,
                Position = new Position(0, 0)
            }).AsTask().Result;

            Assert.AreEqual(0, completionResult.Items.Count());
        }

        private static Regex version = new Regex(@"\d+(\.\d+)*");
    }
}
