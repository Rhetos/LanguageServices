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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// <summary>
    /// The published diagnostics URI must equal the URI of the opened document, as OmniSharp's <see cref="DocumentUri"/> renders it.
    /// The in-process OmniSharp client serializes URIs through <see cref="DocumentUri"/> too, so the client-specific forms
    /// (VS Code's <c>file:///c%3A/...</c>) are tested on the wire by <see cref="DocumentUriWireTests"/>.
    /// </summary>
    [TestClass]
    [DeploymentItem("RhetosAppFolder\\", "RhetosAppFolder")]
    [DeploymentItem("RhetosAppFolder\\FolderWithApp\\MockObj\\Rhetos\\", "RhetosAppFolder\\FolderWithApp\\obj\\Rhetos")]
    [DeploymentItem("DslSyntax.json", "RhetosAppFolder\\FolderWithApp\\obj\\Rhetos\\")]
    public class DocumentUriTests : IntegrationTestBase
    {
        private ILanguageServer server;
        private ILanguageClient client;
        private string tempFolder;

        [TestInitialize]
        public void Initialize()
        {
            tempFolder = Path.Combine(Path.GetTempPath(), "RhetosLanguageServicesUriTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);
            (client, server) = Init();
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                // The client is null if Init failed; the test then reports the Init error.
                if (client != null)
                {
                    client.SendShutdown(new ShutdownParams());
                    client.SendExit();
                    server.WaitForExit.Wait(2000);
                }
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }

        [TestMethod]
        public void PublishedUriEqualsOpenedUri()
        {
            if (!DocumentUriTools.HasDriveLetter(tempFolder))
                Assert.Inconclusive($"The temporary folder '{tempFolder}' does not start with a drive letter.");

            var documents = new[]
                {
                    DocumentUri.Parse(DocumentUriTools.ToVsCodeFileUri(CreateFile(@"encoded-colon\doc.rhe"))),
                    DocumentUri.Parse(DocumentUriTools.ToVisualStudioFileUri(CreateFile(@"upper-drive\doc.rhe"))),
                    DocumentUri.Parse(DocumentUriTools.ToVsCodeFileUri(CreateFile(@"with space\doc file.rhe"))),
                    DocumentUri.Parse(DocumentUriTools.ToVsCodeFileUri(CreateFile(@"čćž\dokument čćž.rhe"))),
                }
                .Select(uri => new TextDocumentItem { LanguageId = "rhetos-dsl", Uri = uri, Text = "Module M;" })
                .ToList();

            foreach (var document in documents)
            {
                Console.WriteLine($"Opening '{document.Uri}'.");
                client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams { TextDocument = document });
            }

            foreach (var document in documents)
            {
                var diagnostics = WaitForDiagnostics(document.Uri.ToString(), list => list.Count > 0);
                Console.WriteLine($"Diagnostics for '{document.Uri}': {string.Join(" | ", diagnostics.Select(d => d.Message))}");
                // There is no Rhetos app in the temporary folder, so each document gets the warning about the missing root path.
                Assert.IsTrue(diagnostics.Any(d => d.Message.Contains("No valid RhetosProjectRootPath", StringComparison.Ordinal)), document.Uri.ToString());
            }

            CollectionAssert.AreEquivalent(documents.Select(d => d.Uri.ToString()).ToList(), GetPublishedDiagnosticsUris().ToList());

            foreach (var document in documents)
                client.TextDocument.DidCloseTextDocument(new DidCloseTextDocumentParams { TextDocument = document.Uri });

            foreach (var document in documents)
                WaitForDiagnostics(document.Uri.ToString(), list => list.Count == 0);

            CollectionAssert.AreEquivalent(documents.Select(d => d.Uri.ToString()).ToList(), GetPublishedDiagnosticsUris().ToList());
        }

        [TestMethod]
        [DataRow("// <rhetosProjectRootPath=\".\\\"/>")]
        [DataRow("// <rhetosProjectRootPath=\"RhetosAppFolder\\FolderWithApp\"/>")] // Relative to the working directory, this would be a valid Rhetos app.
        public void UntitledDocumentWithRelativeDirective(string text)
        {
            var document = new TextDocumentItem { LanguageId = "rhetos-dsl", Uri = DocumentUri.Parse("untitled:Untitled-1"), Text = text };
            client.TextDocument.DidOpenTextDocument(new DidOpenTextDocumentParams { TextDocument = document });

            var diagnostics = WaitForDiagnostics(document.Uri.ToString(), list => list.Count > 0);
            Thread.Sleep(1500); // Let the project monitor run, to check that no Rhetos app is initialized from the working directory.
            diagnostics = GetPublishedDiagnostics(document.Uri.ToString());
            DumpLogs(ServerLogs, "Server Logs");
            Console.WriteLine($"Diagnostics for '{document.Uri}': {string.Join(" | ", diagnostics.Select(d => d.Message))}");

            Assert.AreEqual("untitled:Untitled-1", document.Uri.ToString());
            Assert.AreEqual(1, diagnostics.Count);
            Assert.AreEqual(DiagnosticSeverity.Warning, diagnostics[0].Severity);
            StringAssert.Contains(diagnostics[0].Message, "No valid RhetosProjectRootPath configuration was found for this document (the document 'untitled:Untitled-1' is not a file", StringComparison.Ordinal);
            Assert.IsFalse(server.GetRequiredService<RhetosProjectContext>().IsInitialized);
            Assert.AreEqual(0, CountContainsAll(ServerLogs, "[Error]") + CountContainsAll(ServerLogs, "[Critical]"));
        }

        private string CreateFile(string relativePath)
        {
            var path = Path.Combine(tempFolder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "Module M;");
            return path;
        }

        private IReadOnlyList<Diagnostic> WaitForDiagnostics(string documentUri, Func<IReadOnlyList<Diagnostic>, bool> condition)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var diagnostics = GetPublishedDiagnostics(documentUri);
                if (diagnostics != null && condition(diagnostics))
                    return diagnostics;

                if (stopwatch.Elapsed > TimeSpan.FromSeconds(10))
                    throw new AssertFailedException($"Expected diagnostics were not published for '{documentUri}'."
                        + $" Received diagnostics for: {string.Join(", ", GetPublishedDiagnosticsUris())}.");

                Thread.Sleep(100);
            }
        }
    }

    internal static class DocumentUriTools
    {
        public static bool HasDriveLetter(string path) => path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\';

        /// <summary>
        /// VS Code (vscode-uri) lowercases the drive letter, percent-encodes the drive colon as <c>%3A</c>, and percent-encodes spaces and non-ASCII characters.
        /// </summary>
        public static string ToVsCodeFileUri(string path) => $"file:///{char.ToLowerInvariant(path[0])}%3A{EncodePath(path)}";

        /// <summary>
        /// Visual Studio sends the drive letter in upper case with an unencoded colon.
        /// </summary>
        public static string ToVisualStudioFileUri(string path) => $"file:///{char.ToUpperInvariant(path[0])}:{EncodePath(path)}";

        private static string EncodePath(string path) => string.Join("/", path.Substring(2).Split('\\').Select(Uri.EscapeDataString));
    }
}
