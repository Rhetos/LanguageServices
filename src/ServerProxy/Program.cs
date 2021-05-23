using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog;
using NLog.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ServerProxy
{
    // https://docs.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension?view=vs-2019
    // https://stackoverflow.com/questions/51180046/include-whole-folder-in-vsix
    class Program
    {
        static void Main(string[] args)
        {
            var log = LogManager.GetLogger("StaticLogger");
            log.Info("Starting!");

            var server = LanguageServer.Create(o =>
            {
                o.WithInput(Console.OpenStandardInput());
                o.WithOutput(Console.OpenStandardOutput());
                o.ConfigureLogging(c =>
                {
                    //c.AddLanguageProtocolLogging();
                    //c.AddProvider(new NLogLoggerProvider());
                    c.SetMinimumLevel(LogLevel.Debug);
                });
                o.OnInitialize((languageServer, request, token) =>
                {
                    log.Info("OnInitialize");
                    return Task.CompletedTask;
                });
                o.OnInitialized((languageServer, request, response, token) =>
                {
                    log.Info("OnInitialized");
                    response.Capabilities.TextDocumentSync.Kind = TextDocumentSyncKind.Full;
                    response.Capabilities.TextDocumentSync.Options.Change = TextDocumentSyncKind.Full;
                    return Task.CompletedTask;
                });
                
                o.WithServices(x => x.AddLogging(b =>
                {
                    //b.AddLanguageProtocolLogging();
                    b.SetMinimumLevel(LogLevel.Trace);
                }));
                
                o.OnStarted((languageServer, token) =>
                {
                    log.Info("OnStarted");
                    return Task.CompletedTask;
                });
            });

            log.Info("Hello!");
            //server.Services.GetRequiredService<>()
            server.LogInfo("Created, starting initialize.");
            server.Initialize(new CancellationToken()).Wait();
            server.WaitForExit.Wait();

            return;

            var loggerFactory = LoggerFactory.Create(a => a.AddConsole());

            var processStartInfo = new ProcessStartInfo("C:\\Projects\\Rhetos\\Rhetos.LanguageServices\\src\\Server\\bin\\Debug\\Rhetos.LanguageServices.Server.exe")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var process = Process.Start(processStartInfo);
            LanguageClient client = null;
            try
            {
                client = LanguageClient.Create(o =>
                {
                    o.Trace = InitializeTrace.Verbose;
                    o.ConfigureLogging(l => l.AddConsole());
                    o.WithInput(process.StandardOutput.BaseStream);
                    o.WithOutput(process.StandardInput.BaseStream);
                    o.OnLogMessage(l =>
                    {
                        Console.WriteLine($"SERVER: [{l.Type}] {l.Message}");
                    });
                    
                    o.OnTelemetryEvent(p =>
                    {
                        Console.WriteLine("Received telemetry");
                    });
                    o.OnPublishDiagnostics(p =>
                    {
                        Console.WriteLine($"Received diagnostics");
                    });
                });

                

                client.Initialize(new CancellationToken()).Wait();

                Thread.Sleep(50000);

            }
            finally
            {
                client?.Dispose();
            }


        }

        private static void MyHandler(Uri documenturi, List<Diagnostic> diagnostics)
        {
            throw new NotImplementedException();
        }
    }
}
