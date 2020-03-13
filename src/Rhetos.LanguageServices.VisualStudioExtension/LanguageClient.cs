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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json;

namespace Rhetos.LanguageServices.VisualStudioExtension
{
    [ContentType("rhe")]
    [Export(typeof(ILanguageClient))]
    public class LanguageClient : ILanguageClient
    {
        // allow redirection of LSP server to another path for integration, debugging and testing purposes
        private static readonly string _languageSeverPathConfigurationFilename = "rhetos.lsp-server.local.json";
        public class RhetosLspServerOptions
        {
            public string ServerPath { get; set; }
        }

        public string Name => "Rhetos DSL Language Extension";

        public IEnumerable<string> ConfigurationSections => null;

        public object InitializationOptions => null;

        public IEnumerable<string> FilesToWatch => null;

        public event AsyncEventHandler<EventArgs> StartAsync;
#pragma warning disable CS0067 // The event 'LanguageClient.StopAsync' is never used
        public event AsyncEventHandler<EventArgs> StopAsync;
#pragma warning restore CS0067

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            await Task.Yield();

            try
            {
                var serverProcess = StartLanguageServer();
                if (serverProcess != null)
                    return new Connection(serverProcess.StandardOutput.BaseStream, serverProcess.StandardInput.BaseStream);

                return null;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Error activating Language Server Process. {e}");
                throw;
            }
        }

        private Process StartLanguageServer()
        {
            var languageServerPath = GetLanguageServerPath();
            Trace.WriteLine($"Starting language server at: '{languageServerPath}'.");

            var info = new ProcessStartInfo
            {
                FileName = languageServerPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = info };

            if (process.Start())
                return process;

            return null;
        }

        private string GetLanguageServerPath()
        {
            var languageServerPath = TryReadPathConfigurationFile()?.ServerPath;

            if (!string.IsNullOrEmpty(languageServerPath))
                return languageServerPath;

            var codebaseLocalPath = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var extensionFolder = Path.GetDirectoryName(codebaseLocalPath);
            languageServerPath = Path.Combine(extensionFolder, "Rhetos.LanguageServices.Server.exe");
            return languageServerPath;
        }

        private RhetosLspServerOptions TryReadPathConfigurationFile()
        {
            if (!File.Exists(_languageSeverPathConfigurationFilename))
                return null;
            
            var options = JsonConvert.DeserializeObject<RhetosLspServerOptions>(File.ReadAllText(_languageSeverPathConfigurationFilename));
            Trace.WriteLine($"Loaded server path from path configuration file: '{options.ServerPath}'.");
            return options;
        }

        public async Task OnLoadedAsync()
        {
            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public Task OnServerInitializeFailedAsync(Exception e)
        {
            return Task.CompletedTask;
        }
    }
}
