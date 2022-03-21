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
                    // Give it one last chance to gracefully exit
                    Task.Delay(1000, cancellationToken).Wait(cancellationToken);
                    nLogLogger.Warn($"Host process error, self-terminating: {e}.");

                    LogManager.Flush();
                    LogManager.Shutdown();

                    Environment.Exit(2);
                }
            }
        }
    }
}
