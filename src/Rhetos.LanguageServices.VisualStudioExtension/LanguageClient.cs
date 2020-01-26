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

            var options = JsonConvert.DeserializeObject<RhetosLspServerOptions>(File.ReadAllText("rhetos.lsp-server.local.json"));
            Trace.WriteLine($"Loaded server path from options: '{options.ServerPath}'.");

            var info = new ProcessStartInfo
            {
                FileName = options.ServerPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process {StartInfo = info};

            if (process.Start())
            {
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }

            return null;
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
