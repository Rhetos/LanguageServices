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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Rhetos.LanguageServices.CodeAnalysis.Services;

namespace Rhetos.LanguageServices.Server.Services
{
    public class RhetosProjectMonitor
    {
        private static readonly TimeSpan _cycleInterval = TimeSpan.FromMilliseconds(1000);
        private readonly Lazy<RhetosWorkspace> rhetosWorkspace;
        private readonly ILogger<RhetosProjectMonitor> log;

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Task monitorLoopTask;

        public RhetosProjectMonitor(ILanguageServerFacade languageServerFacade, ILogger<RhetosProjectMonitor> log)
        {
            this.rhetosWorkspace = new Lazy<RhetosWorkspace>(languageServerFacade.GetRequiredService<RhetosWorkspace>);
            this.log = log;
        }

        public void Start()
        {
            if (cancellationTokenSource.IsCancellationRequested)
                return;

            log.LogInformation($"Starting {nameof(RhetosProjectMonitor)}.{nameof(MonitorLoop)}.");
            monitorLoopTask = Task.Factory.StartNew(() => MonitorLoop(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            try
            {
                log.LogDebug($"Stopping {nameof(RhetosProjectMonitor)}.{nameof(MonitorLoop)}.");
                cancellationTokenSource.Cancel();
                monitorLoopTask?.Wait();
            }
            catch (Exception e)
            {
                if (e is AggregateException aggregateException && aggregateException.InnerExceptions.Any(inner => !(inner is TaskCanceledException)))
                    log.LogDebug($"{nameof(MonitorLoop)} successfully cancelled.");
                else
                    log.LogDebug($"{nameof(MonitorLoop)} faulted while waiting to cancel: {monitorLoopTask?.Exception}");
            }
        }

        public void MonitorLoop(CancellationToken cancellationToken)
        {
            while (true)
            {
                Task.Delay(_cycleInterval, cancellationToken).Wait(cancellationToken);

                try
                {
                    rhetosWorkspace.Value.UpdateRhetosProjectContext();
                }
                catch (Exception e)
                {
                    log.LogWarning($"Error occurred during monitoring RhetosAppContext state: {e}");
                }
            }
        }
    }
}
