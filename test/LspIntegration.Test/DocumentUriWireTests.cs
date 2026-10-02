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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Rhetos.LanguageServices.Server;

namespace Rhetos.LanguageServices.LspIntegration.Test
{
    /// <summary>
    /// Runs the server as a separate process and talks raw JSON-RPC over stdin/stdout, as VS Code does.
    /// The in-process OmniSharp test client cannot send VS Code's URI form, because it serializes URIs through <see cref="DocumentUri"/>.
    /// </summary>
    [TestClass]
    public class DocumentUriWireTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

        [TestMethod]
        public void VsCodeEncodedDriveColon()
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), "RhetosLanguageServicesWireTests-" + Guid.NewGuid().ToString("N"));
            if (!DocumentUriTools.HasDriveLetter(tempFolder))
                Assert.Inconclusive($"The temporary folder '{tempFolder}' does not start with a drive letter.");

            Process process = null;
            BlockingCollection<string> messages = null;
            try
            {
                var documentPaths = new[] { Path.Combine(tempFolder, "wire.rhe"), Path.Combine(tempFolder, "čćž folder", "wire čćž.rhe") };
                foreach (var documentPath in documentPaths)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(documentPath));
                    File.WriteAllText(documentPath, "Module M;");
                }
                var sentUris = documentPaths.Select(DocumentUriTools.ToVsCodeFileUri).ToList();

                process = StartServer();
                messages = StartReadingMessages(process.StandardOutput.BaseStream);
                process.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine($"[server stderr] {e.Data}"); };
                process.BeginErrorReadLine();

                Send(process, new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "initialize",
                    @params = new
                    {
                        processId = Environment.ProcessId,
                        rootUri = (string)null,
                        // The server requires capabilities.textDocument.synchronization (known issue in RhetosLanguageServer.OnInitializedAsync).
                        capabilities = new { textDocument = new { synchronization = new { dynamicRegistration = false }, publishDiagnostics = new { } } },
                    }
                });
                WaitForMessage(process, messages, message => (int?)message["id"] == 1 && message["method"] == null);

                Send(process, new { jsonrpc = "2.0", method = "initialized", @params = new { } });
                foreach (var sentUri in sentUris)
                    Send(process, new
                    {
                        jsonrpc = "2.0",
                        method = "textDocument/didOpen",
                        @params = new { textDocument = new { uri = sentUri, languageId = "rhetos-dsl", version = 1, text = "Module M;" } }
                    });

                // OmniSharp parses the incoming URI into DocumentUri parts (the original string is not kept), and writes outgoing URIs
                // with DocumentUri.ToString(), which renders the drive colon unencoded ('file:///c:/...').
                // A byte-exact echo of VS Code's 'c%3A' is therefore not possible with OmniSharp's typed messages.
                // The A3 acceptance criterion is that the server publishes exactly this canonical form of the opened document's URI.
                var expectedUris = sentUris.Select(sentUri => DocumentUri.Parse(sentUri).ToString()).ToList();
                var publishedUris = new List<string>();
                while (!expectedUris.All(publishedUris.Contains))
                {
                    var publish = WaitForMessage(process, messages, message => (string)message["method"] == "textDocument/publishDiagnostics");
                    var publishedUri = (string)publish["params"]["uri"];
                    Console.WriteLine($"Published URI: {publishedUri}");
                    CollectionAssert.Contains(expectedUris, publishedUri);
                    Assert.IsFalse(publishedUri.Contains("%3A", StringComparison.OrdinalIgnoreCase), publishedUri);
                    Assert.IsTrue(((JArray)publish["params"]["diagnostics"]).Count > 0, "Expected the warning about the missing Rhetos project root path.");
                    publishedUris.Add(publishedUri);
                }

                for (var i = 0; i < sentUris.Count; i++)
                    Console.WriteLine($"Sent URI:     {sentUris[i]}{Environment.NewLine}Expected URI: {expectedUris[i]}");

                Send(process, new { jsonrpc = "2.0", id = 2, method = "shutdown" });
                WaitForMessage(process, messages, message => (int?)message["id"] == 2 && message["method"] == null);
                Send(process, new { jsonrpc = "2.0", method = "exit" });
                if (!process.WaitForExit((int)_timeout.TotalMilliseconds))
                    Console.WriteLine($"The server did not exit within {_timeout.TotalSeconds} s after the 'exit' notification.");
            }
            finally
            {
                if (process != null)
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(true);
                    }
                    catch (InvalidOperationException)
                    {
                        // The process has already exited.
                    }
                    process.Dispose();
                }

                messages?.Dispose();

                try
                {
                    if (Directory.Exists(tempFolder))
                        Directory.Delete(tempFolder, true);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // Do not hide the test result; the folder is under the temporary folder.
                    Console.WriteLine($"Cannot delete the temporary folder '{tempFolder}': {e.Message}");
                }
            }
        }

        private static Process StartServer()
        {
            var serverFolder = Path.GetDirectoryName(typeof(RhetosLanguageServer).Assembly.Location);
            var serverDll = Path.Combine(serverFolder, "Rhetos.LanguageServices.Server.dll");
            if (!File.Exists(Path.Combine(serverFolder, "Rhetos.LanguageServices.Server.runtimeconfig.json")))
                Assert.Inconclusive($"The server runtime configuration file is missing in '{serverFolder}'.");

            // 'dotnet test' sets DOTNET_HOST_PATH to the dotnet executable that runs the tests.
            var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (string.IsNullOrEmpty(dotnet) || !File.Exists(dotnet))
                dotnet = "dotnet";

            var startInfo = new ProcessStartInfo(dotnet)
            {
                ArgumentList = { serverDll },
                WorkingDirectory = serverFolder,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            };

            try
            {
                Console.WriteLine($"Starting '{dotnet} {serverDll}'.");
                return Process.Start(startInfo);
            }
            catch (Win32Exception e)
            {
                Assert.Inconclusive($"Cannot start '{dotnet}': {e.Message}");
                throw;
            }
        }

        private static void Send(Process process, object message)
        {
            var json = JsonConvert.SerializeObject(message);
            Console.WriteLine($"--> {json}");
            var content = Encoding.UTF8.GetBytes(json);
            var header = Encoding.ASCII.GetBytes($"Content-Length: {content.Length}\r\n\r\n");
            var stream = process.StandardInput.BaseStream;
            stream.Write(header, 0, header.Length);
            stream.Write(content, 0, content.Length);
            stream.Flush();
        }

        /// <summary>
        /// Reads Content-Length framed messages on a background thread. The collection is completed when the stream ends.
        /// </summary>
        private static BlockingCollection<string> StartReadingMessages(Stream stream)
        {
            var messages = new BlockingCollection<string>();
            var thread = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        var contentLength = ReadHeaders(stream);
                        if (contentLength == null)
                            break;

                        var content = new byte[contentLength.Value];
                        stream.ReadExactly(content, 0, content.Length);
                        messages.Add(Encoding.UTF8.GetString(content));
                    }
                }
                catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is EndOfStreamException || e is InvalidOperationException)
                {
                    Console.WriteLine($"Stopped reading server output: {e.GetType().Name}.");
                }
                finally
                {
                    try { messages.CompleteAdding(); } catch (ObjectDisposedException) { }
                }
            })
            { IsBackground = true };
            thread.Start();
            return messages;
        }

        private static int? ReadHeaders(Stream stream)
        {
            int? contentLength = null;
            while (true)
            {
                var line = new StringBuilder();
                while (true)
                {
                    var b = stream.ReadByte();
                    if (b < 0)
                        return null;
                    if (b == '\n')
                        break;
                    if (b != '\r')
                        line.Append((char)b);
                }

                if (line.Length == 0)
                    return contentLength ?? throw new InvalidOperationException("Missing Content-Length header.");

                var header = line.ToString();
                if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    contentLength = int.Parse(header.Substring("Content-Length:".Length).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static JObject WaitForMessage(Process process, BlockingCollection<string> messages, Func<JObject, bool> condition)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var remaining = _timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero || !messages.TryTake(out var json, remaining))
                    throw new AssertFailedException($"Expected message was not received within {_timeout.TotalSeconds} s"
                        + (messages.IsCompleted ? " (the server output ended)." : "."));

                Console.WriteLine($"<-- {json}");
                var message = JObject.Parse(json);

                // Answer requests from the server (they have both 'id' and 'method'), so that the server does not wait for the client.
                if (message["id"] != null && message["method"] != null)
                    Send(process, new { jsonrpc = "2.0", id = message["id"], result = (object)null });

                if (condition(message))
                    return message;
            }
        }
    }
}
