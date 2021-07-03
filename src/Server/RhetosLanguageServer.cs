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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NLog;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Targets.Wrappers;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
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
        public static ILanguageServer Instance { get; private set; }

        private readonly ILogger hostLog;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public RhetosLanguageServer(ILogger hostLog)
        {
            this.hostLog = hostLog;
        }

        public async Task Run()
        {
            hostLog.Info($"SERVER START");

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
                hostLog.Error($"Exception running language server: {e}");
                throw;
            }

            hostLog.Info($"SERVER END");
        }

        private async Task RunAndWaitExit(Stream input, Stream output)
        {
            if (Instance != null)
                throw new InvalidOperationException($"RhetosLanguage server has already been initialized. Two instances is not allowed.");

            Instance = BuildLanguageServer(input, output);
            AddServerCleanupHandlers(Instance, hostLog);

            hostLog.Debug("Language Server built. Awaiting initialize...");
            await Instance.Initialize(cancellationTokenSource.Token);
            hostLog.Debug("Initialized!");

            await Instance.WaitForExit;
        }

        public static void AddServerCleanupHandlers(ILanguageServer server, ILogger log = null)
        {
            server.Shutdown.Subscribe(next =>
            {
                log?.Debug($"Shutdown requested.");
                server.Services.GetRequiredService<PublishDiagnosticsRunner>().Stop();
                server.Services.GetRequiredService<RhetosProjectMonitor>().Stop();
            });

            server.Exit.Subscribe(next =>
            {
                log?.Info($"Exit requested: {next}");
                server.Services.GetRequiredService<OrphanedProcessMonitor>().Stop();
            });
        }

        public static ILanguageServer BuildLanguageServer(Stream inputStream, Stream outputStream)
        {
            var server = LanguageServer.Create(options =>
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
            // Hack to circumvent OmniSharp bug: https://github.com/OmniSharp/csharp-language-server-protocol/issues/609
            options.Services.AddSingleton<IReceiver, RhetosJsonRpcReceiver>();

            options
                .WithHandler<TextDocumentHandler>()
                .WithHandler<RhetosHoverHandler>()
                .WithHandler<RhetosSignatureHelpHandler>()
                .WithHandler<RhetosCompletionHandler>()
                .WithUnhandledExceptionHandler(UnhandledExceptionHandler)
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

            // Enable all - SetMinimumLevel above will not work, because OmniSharp does .AddLogging after it, which resets default min level
            builder.AddFilter("*", LogLevel.Trace);

            // Filter other logs outgoing via LSP to VS2019
            builder.Services.Configure<LoggerFilterOptions>(o => o.Rules.Add(
                new LoggerFilterRule(
                    "OmniSharp.Extensions.LanguageServer.Server.Logging.LanguageServerLoggerProvider",
                    "Rhetos.LanguageServices.*", LogLevel.Information, null)));
            builder.Services.Configure<LoggerFilterOptions>(o => o.Rules.Add(
                new LoggerFilterRule(
                    "OmniSharp.Extensions.LanguageServer.Server.Logging.LanguageServerLoggerProvider",
                    "*", LogLevel.Warning, null)));

            // With our custom RhetosJsonRpcReceiver we have messed up IReceiver registration order.
            // As a result LspServerOutputFilter incorrectly emits warning messages. We are completely hiding those.
            builder.Services.Configure<LoggerFilterOptions>(o => o.Rules.Add(
                new LoggerFilterRule(
                    "OmniSharp.Extensions.LanguageServer.Server.Logging.LanguageServerLoggerProvider",
                    "OmniSharp.Extensions.LanguageServer.Server.LspServerOutputFilter", LogLevel.Error, null)));

            builder.Services.Configure<LoggerFilterOptions>(o => o.Rules.Add(
                new LoggerFilterRule(
                    typeof(NLogLoggerProvider).FullName,
                    "OmniSharp.Extensions.LanguageServer.Server.LspServerOutputFilter", LogLevel.Error, null)));

            // Warning about no configuration surfacing. Not sure what it means.
            // We will hide it from LSP logging, but keep it in NLog.
            builder.Services.Configure<LoggerFilterOptions>(o => o.Rules.Add(
                new LoggerFilterRule(
                    "OmniSharp.Extensions.LanguageServer.Server.Logging.LanguageServerLoggerProvider",
                    "OmniSharp.Extensions.LanguageServer.Server.Configuration.DidChangeConfigurationProvider", LogLevel.Error, null)));

            // No filtering for NLog provider. We can specify rules for those in NLog.config
        }

        public static void ConfigureLoggingExplicitFilters(ILoggingBuilder builder, LogLevel globalMinLevel = LogLevel.Warning, LogLevel rhetosMinLevel = LogLevel.Information)
        {
            builder.AddFilter((s, level) =>
                s.StartsWith("Rhetos.LanguageServices")
                    ? level >= rhetosMinLevel
                    : level >= globalMinLevel);
        }

        private static void UnhandledExceptionHandler(Exception e)
        {
            var log = Instance?.Services.GetRequiredService<ILogger<RhetosLanguageServer>>();
            log?.LogError($"Unhandled exception in LanguageServer: {e}");
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // used by handlers
            services.AddSingleton<RhetosWorkspace>();
            services.AddSingleton<ConceptQueries>();
            services.AddSingleton<RhetosDocumentFactory>();
            services.AddSingleton<RhetosProjectContext>();
            services.AddSingleton<IRhetosProjectRootPathResolver, RhetosProjectRootPathResolver>();

            //services.AddTransient<ServerEventHandler>();
            services.AddSingleton<ILogProvider, RhetosNetCoreLogProvider>();
            services.AddSingleton<PublishDiagnosticsRunner>();
            services.AddSingleton<RhetosProjectMonitor>();
            services.AddSingleton<OrphanedProcessMonitor>();
        }

        private static Task OnInitializeAsync(ILanguageServer languageServer, InitializeParams request, CancellationToken cancellationToken)
        {
            var log = languageServer.Services.GetRequiredService<ILoggerFactory>().CreateLogger<RhetosLanguageServer>();
            var logFileMessage = GetLogFilePath();
            if (string.IsNullOrEmpty(logFileMessage))
                logFileMessage = "No log file configuration found. Edit 'NLog.config' to add log file target.";
            else
                logFileMessage = $"Log file: '{logFileMessage}'.";

            var localPath = new Uri(Assembly.GetExecutingAssembly().Location).LocalPath;
            log.LogInformation($"Initialized. Running server '{localPath}'. {logFileMessage}");

            return Task.CompletedTask;
        }

        private static Task OnInitializedAsync(ILanguageServer languageServer, InitializeParams request, InitializeResult response, CancellationToken cancellationToken)
        {
            response.Capabilities.TextDocumentSync.Kind = TextDocumentSyncKind.Full;
            response.Capabilities.TextDocumentSync.Options!.Change = TextDocumentSyncKind.Full;
            languageServer.Services.GetRequiredService<PublishDiagnosticsRunner>().Start();
            languageServer.Services.GetRequiredService<RhetosProjectMonitor>().Start();

            if (request.ProcessId.HasValue)
            {
                var orphanedProcessMonitor = languageServer.Services.GetRequiredService<OrphanedProcessMonitor>();
                orphanedProcessMonitor.SetHostProcessId((int)request.ProcessId.Value);
                orphanedProcessMonitor.Start();
            }
            
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
