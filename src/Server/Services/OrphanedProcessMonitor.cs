using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NLog;

namespace Rhetos.LanguageServices.Server.Services
{
    public class OrphanedProcessMonitor
    {
        private readonly ILogger<OrphanedProcessMonitor> log;
        private static readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(3);
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Task checkTask;
        private int hostProcessId = -1;
        private Process hostProcess;
        private readonly Logger nLogLogger;

        public OrphanedProcessMonitor(ILogger<OrphanedProcessMonitor> log)
        {
            this.log = log;

            // don't log to LSP pipeline logger to avoid exceptions while trying to communicate with server
            nLogLogger = LogManager.GetCurrentClassLogger();
        }

        public void SetHostProcessId(int processId)
        {
            if (hostProcessId != -1 && hostProcessId != processId)
                throw new InvalidOperationException($"Host Process Id has already been set.");

            hostProcessId = processId;
            log.LogDebug($"Host Process Id set to {hostProcessId}.");
        }

        public void Start()
        {
            if (cancellationTokenSource.IsCancellationRequested)
                return;

            if (checkTask != null)
                throw new InvalidOperationException("Already started.");

            log.LogDebug($"Starting {nameof(OrphanedProcessMonitor)}.");
            checkTask = Task.Factory.StartNew(() => CheckLoop(cancellationTokenSource.Token), cancellationTokenSource.Token, TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void Stop()
        {
            try
            {
                nLogLogger.Debug($"Stopping {nameof(OrphanedProcessMonitor)}.");
                cancellationTokenSource.Cancel();
                checkTask?.Wait();
            }
            catch (Exception e)
            {
                if (e is AggregateException aggregateException && aggregateException.InnerExceptions.Any(inner => !(inner is TaskCanceledException)))
                    nLogLogger.Debug($"{nameof(OrphanedProcessMonitor)} successfully cancelled.");
                else
                    nLogLogger.Debug($"{nameof(OrphanedProcessMonitor)} faulted while waiting to cancel: {checkTask?.Exception}");
            }

        }

        public void CheckLoop(CancellationToken cancellationToken)
        {
            while (true)
            {
                Task.Delay(_checkInterval, cancellationToken).Wait(_checkInterval);

                try
                {
                    if (hostProcess == null)
                    {
                        if (hostProcessId == -1)
                            continue;

                        hostProcess = Process.GetProcessById(hostProcessId);
                        log.LogTrace($"Retrieved host process with id={hostProcessId}.");
                    }

                    if (hostProcess.HasExited)
                        throw new InvalidOperationException("Host exited, need to self-terminate.");

                }
                catch (Exception e)
                {
                    nLogLogger.Warn($"Host process error, self-terminating: {e}.");

                    LogManager.Flush();
                    LogManager.Shutdown();

                    Environment.Exit(2);
                }
            }
        }
    }
}
