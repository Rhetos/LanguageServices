using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog;
using NLog.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Server;
using Rhetos.LanguageServices.Server.Handlers;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Rhetos.LanguageServices.Server
{
    public static class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        static async Task MainAsync(string[] args)
        {
            var programLogger = LogManager.GetLogger("Program");
            programLogger.Info("Program START");
            /*
            Debugger.Launch();
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                await Task.Delay(100);
            }*/

            var server = await BuildLanguageServer(Console.OpenStandardInput(), Console.OpenStandardOutput(),
                builder => builder
                    .AddNLog()
                    .AddLanguageServer(LogLevel.Information)
                    .SetMinimumLevel(LogLevel.Debug)
            );

            server.Shutdown.Subscribe(next =>
            {
                programLogger.Info($"SHUTDOWN SUBSCRIBE {next}");
                Task.Delay(10000).Wait();
            });

            server.Exit.Subscribe(next =>
            {
                programLogger.Info($"EXIT SUBSCRIBE {next}");
            });

            await server.WaitForExit;

            programLogger.Info("Program END");
            LogManager.Flush();
        }

        public static async Task<ILanguageServer> BuildLanguageServer(Stream inputStream, Stream outputStream, Action<ILoggingBuilder> logBuilderAction)
        {
            var server = await LanguageServer.From(options =>
                options
                    .WithInput(inputStream)
                    .WithOutput(outputStream)
                    .ConfigureLogging(logBuilderAction)
                    .WithHandler<TextDocumentHandler>()
                    //.WithHandler<RhetosHoverHandler>()
                    //.WithReciever(new DebugReceiver())
                    .WithHandler<RhetosCompletionHandler>()
                    //.WithHandler<DidChangeWatchedFilesHandler>()
                    .WithServices(services =>
                    {
                        services.AddTransient<ServerEventHandler>();
                        services.AddSingleton<TrackedDocuments>();
                        services.AddSingleton<RhetosAppContext>();
                        /*
                        services.AddSingleton<Foo>(provider => 
                        {
                            var loggerFactory = provider.GetService<ILoggerFactory>();
                            var logger = loggerFactory.CreateLogger<Foo>();

                            logger.LogInformation("Configuring");

                            return new Foo(logger);
                        
                        });*/
                    })
                    .OnInitialized((srv, request, response) =>
                    {
                        response.Capabilities.TextDocumentSync.Kind = TextDocumentSyncKind.Full;
                        response.Capabilities.TextDocumentSync.Options.Change = TextDocumentSyncKind.Full;
                        return Task.CompletedTask;
                    })
                    .OnInitialize((s, request) =>
                    {
                        //LogManager.GetLogger("Init").Info(JsonConvert.SerializeObject(request, Formatting.Indented));
                        return Task.CompletedTask;
                    })
            );

            return server;
        }
    }
}

