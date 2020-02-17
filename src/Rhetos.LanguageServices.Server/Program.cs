using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog;
using NLog.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Server;
using Rhetos.LanguageServices.Server.Handlers;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Rhetos.LanguageServices.Server
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var programLogger = LogManager.GetLogger("Program");
            programLogger.Info($"SERVER START");

            try
            {
                using (var input = Console.OpenStandardInput())
                using (var output = Console.OpenStandardOutput())
                {
                    await RunAndWaitExit(programLogger, input, output);
                }
            }
            catch (Exception e)
            {
                programLogger.Error($"Exception running language server: {e}");
            }

            programLogger.Info($"SERVER END");
            LogManager.Flush();
        }

        public static async Task RunAndWaitExit(Logger programLogger, Stream input, Stream output)
        {
            var server = await BuildLanguageServer(input, output,
                builder => builder
                    .AddNLog()
                    .AddLanguageServer(LogLevel.Information)
                    .SetMinimumLevel(LogLevel.Debug)
            );

            programLogger.Info("Language Server built and started.");

            server.Shutdown.Subscribe(next =>
            {
                programLogger.Info($"Shutdown requested: {next}");
                Task.Delay(500).Wait();
            });

            server.Exit.Subscribe(next =>
            {
                programLogger.Info($"Exit requested: {next}");
            });

            await server.WaitForExit;
        }

        public static async Task<ILanguageServer> BuildLanguageServer(Stream inputStream, Stream outputStream, Action<ILoggingBuilder> logBuilderAction)
        {
            var server = await LanguageServer.From(options =>
                options
                    .WithInput(inputStream)
                    .WithOutput(outputStream)
                    .ConfigureLogging(logBuilderAction)
                    .WithHandler<TextDocumentHandler>()
                    .WithHandler<RhetosHoverHandler>()
                    .WithHandler<RhetosSignatureHelpHandler>()
                    //.WithReciever(new DebugReceiver())
                    .WithHandler<RhetosCompletionHandler>()
                    //.WithHandler<DidChangeWatchedFilesHandler>()
                    .WithServices(services =>
                    {
                        services.AddTransient<ServerEventHandler>();
                        services.AddSingleton<RhetosWorkspace>();
                        services.AddTransient<RhetosDocumentFactory>();
                        services.AddSingleton<RhetosAppContext>();
                        services.AddSingleton<XmlDocumentationProvider>();
                        services.AddSingleton<ILogProvider, RhetosNetCoreLogProvider>();
                        services.AddSingleton<PublishDiagnosticsRunner>();
                        services.AddSingleton<ConceptQueries>();
                    })
                    .OnInitialize((languageServer, request) =>
                    {
                        var log = languageServer.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Init");
                        log.LogInformation($"Received Init message from client. Logging client capabilities to Debug log.");
                        log.LogDebug(JsonConvert.SerializeObject(request, Formatting.Indented));
                        return Task.CompletedTask;
                    })
                    .OnInitialized((languageServer, request, response) =>
                    {
                        response.Capabilities.TextDocumentSync.Kind = TextDocumentSyncKind.Full;
                        response.Capabilities.TextDocumentSync.Options.Change = TextDocumentSyncKind.Full;
                        languageServer.Services.GetService<PublishDiagnosticsRunner>().Start();
                        return Task.CompletedTask;
                    })
            );

            return server;
        }
    }
}

