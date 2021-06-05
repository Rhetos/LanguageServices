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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.CodeAnalysis.Tools;
using Rhetos.LanguageServices.Server.Services;

namespace Rhetos.LanguageServices.Server.Handlers
{
    [Obsolete("Not used anywhere?")]
    public class ServerEventHandler
    {
        private readonly RhetosProjectContext rhetosProjectContext;
        private readonly ILogger<ServerEventHandler> log;

        public ServerEventHandler(RhetosProjectContext rhetosProjectContext, ILogger<ServerEventHandler> log)
        {
            this.rhetosProjectContext = rhetosProjectContext;
            this.log = log;
        }

        public Task InitializeRhetosContext(string rootPath)
        {
            log.LogInformation($"Initializing RhetosContext with rootPath='{rootPath}'.");
            var initializeTask = Task.Run(() => rhetosProjectContext.Initialize(new DslSyntaxProvider(rootPath)))
                .ContinueWith(result =>
                {
                    var status = result.Status == TaskStatus.RanToCompletion
                        ? "OK"
                        : result.Exception?.Flatten().ToString();
                    log.LogInformation($"Initialize complete with status: {status}.");
                });

            return initializeTask;
        }
    }
}
