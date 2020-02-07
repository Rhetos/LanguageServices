using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NLog;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class RhetosWorkspaceTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly RhetosAppContext rhetosAppContext;

        public RhetosWorkspaceTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            LogManager.Configuration.Reload();
            loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            rhetosAppContext = new RhetosAppContext(loggerFactory.CreateLogger<RhetosAppContext>());
            rhetosAppContext.InitializeFromCurrentDomain();
        }

        [TestMethod]
        public void ParsesTokens()
        {
            var text =
@"Module mad
{'
";
            var logProvider = new NLogProvider();
            /*
            var log = LogManager.GetLogger("NLOG");
            log.Info($"log 1");
            log.Info($"log 2");
            */

            var workspace = new RhetosWorkspace(rhetosAppContext, logProvider, loggerFactory.CreateLogger<RhetosWorkspace>());
            workspace.UpdateDocumentText("bla", text);
            Task.Delay(500).Wait();

            foreach (var pair in workspace.GetAllErrors())
            {
                Console.WriteLine(pair.error.Message);
            }
        }
    }
}
