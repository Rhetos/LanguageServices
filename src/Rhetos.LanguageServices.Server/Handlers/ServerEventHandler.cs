using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using Rhetos.LanguageServices.Server.Services;

namespace Rhetos.LanguageServices.Server.Handlers
{
    public class ServerEventHandler
    {
        private readonly ILanguageServer languageServer;
        private readonly RhetosAppContext rhetosContext;
        private readonly ILogger<ServerEventHandler> log;

        public ServerEventHandler(ILanguageServer languageServer, RhetosAppContext rhetosContext, ILogger<ServerEventHandler> log)
        {
            this.languageServer = languageServer;
            this.rhetosContext = rhetosContext;
            this.log = log;
        }

        public Task InitializeRhetosContext(string rootPath)
        {
            log.LogInformation($"Initializing RhetosContext with rootPath='{rootPath}'.");
            var initializeTask = Task.Run(() => rhetosContext.InitializeFromAppPath(rootPath))
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
