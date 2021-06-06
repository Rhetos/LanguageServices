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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Targets.Wrappers;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.CodeAnalysis.Tools;
using Rhetos.LanguageServices.Server.Handlers;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;
using ILogger = NLog.ILogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Rhetos.LanguageServices.Server
{
    public class RhetosLanguageServer
    {
        private readonly ILogger log;

        public RhetosLanguageServer(ILogger log)
        {
            this.log = log;
        }

        public async Task Run()
        {
            log.Info($"SERVER START");

            try
            {
                using (var input = Console.OpenStandardInput())
                using (var output = Console.OpenStandardOutput())
                {
                    await RunAndWaitExit(input, output);
                }
            }
            catch (Exception e)
            {
                log.Error($"Exception running language server: {e}");
                throw;
            }

            log.Info($"SERVER END");
        }

        private async Task RunAndWaitExit(Stream input, Stream output)
        {
            var server = await BuildLanguageServer(input, output);

            log.Info("Language Server built and started.");

            AddServerCleanupHandlers(server, log);

            await server.WaitForExit;
        }

        public static void AddServerCleanupHandlers(ILanguageServer server, ILogger log = null)
        {
            server.Shutdown.Subscribe(next =>
            {
                log?.Debug($"Shutdown requested.");
                server.Services.GetRequiredService<PublishDiagnosticsRunner>().Stop();
                server.Services.GetRequiredService<RhetosProjectMonitor>().Stop();
                Task.Delay(500).Wait();
            });

            server.Exit.Subscribe(next =>
            {
                log?.Info($"Exit requested: {next}");
            });
        }

        public static async Task<ILanguageServer> BuildLanguageServer(Stream inputStream, Stream outputStream)
        {
            var server = await LanguageServer.From(options =>
            {
                options
                    .WithInput(inputStream)
                    .WithOutput(outputStream);

                ConfigureLanguageServer(options, ConfigureLoggingDefaults);
            });

            return server;
        }

        public static void ConfigureLanguageServer(LanguageServerOptions options, Action<ILoggingBuilder> configureLoggingAction = null)
        {
            options
                .WithHandler<TextDocumentHandler>()
                .WithHandler<RhetosHoverHandler>()
                .WithHandler<RhetosSignatureHelpHandler>()
                .WithHandler<RhetosCompletionHandler>()
                .WithServices(ConfigureServices)
                .OnInitialize(OnInitializeAsync)
                .OnInitialized(OnInitializedAsync);

            if (configureLoggingAction != null)
                options.ConfigureLogging(configureLoggingAction);
        }

        public static void ConfigureLoggingDefaults(ILoggingBuilder builder)
        {
            builder.AddProvider(new NLogLoggerProvider())
                .AddLanguageProtocolLogging()
                .SetMinimumLevel(LogLevel.Trace);

            ConfigureLoggingExplicitFilters(builder);
        }

        public static void ConfigureLoggingExplicitFilters(ILoggingBuilder builder, LogLevel globalMinLevel = LogLevel.Warning, LogLevel rhetosMinLevel = LogLevel.Information)
        {
            builder.AddFilter((s, level) =>
                s.StartsWith("Rhetos.LanguageServices")
                    ? level >= rhetosMinLevel
                    : level >= globalMinLevel);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // used by handlers
            services.AddSingleton<RhetosWorkspace>();
            services.AddSingleton<ConceptQueries>();
            services.AddSingleton<RhetosDocumentFactory>();
            services.AddSingleton<RhetosProjectContext>();
            services.AddSingleton<XmlDocumentationProvider>();
            services.AddSingleton<IRhetosProjectRootPathResolver, RhetosProjectRootPathResolver>();

            //services.AddTransient<ServerEventHandler>();
            services.AddSingleton<ILogProvider, RhetosNetCoreLogProvider>();
            services.AddSingleton<PublishDiagnosticsRunner>();
            services.AddSingleton<RhetosProjectMonitor>();
        }


        private static Task OnInitializeAsync(ILanguageServer languageServer, InitializeParams request, CancellationToken cancellationToken)
        {
            var log = languageServer.Services.GetRequiredService<ILoggerFactory>().CreateLogger<RhetosLanguageServer>();
            var logFileMessage = GetLogFilePath();
            if (string.IsNullOrEmpty(logFileMessage))
                logFileMessage = "No log file configuration found. Edit 'NLog.config' to add log file target.";
            else
                logFileMessage = $"Log file: '{logFileMessage}'.";

            var localPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            log.LogInformation($"Initialized. Running server '{localPath}'. {logFileMessage}");
            // log.LogDebug(JsonConvert.SerializeObject(request, Formatting.Indented));

            return Task.CompletedTask;
        }

        private static Task OnInitializedAsync(ILanguageServer languageServer, InitializeParams request, InitializeResult response, CancellationToken cancellationToken)
        {
            response.Capabilities.TextDocumentSync.Kind = TextDocumentSyncKind.Full;
            response.Capabilities.TextDocumentSync.Options!.Change = TextDocumentSyncKind.Full;
            languageServer.Services.GetRequiredService<PublishDiagnosticsRunner>().Start();
            languageServer.Services.GetRequiredService<RhetosProjectMonitor>().Start();

            return Task.CompletedTask;
        }

        private static string GetLogFilePath()
        {
            try
            {
                var fileTarget = LogManager.Configuration.LoggingRules
                    .SelectMany(rule => rule.Targets)
                    .Select(target => (target is AsyncTargetWrapper wrapper) ? wrapper.WrappedTarget : target)
                    .OfType<FileTarget>()
                    .FirstOrDefault();

                if (fileTarget == null)
                    return null;

                var logEventInfo = new LogEventInfo {TimeStamp = DateTime.Now};
                var fileName = fileTarget.FileName.Render(logEventInfo);
                return Path.GetFullPath(fileName);
            }
            catch
            {
                return null;
            }
        }
    }
}
