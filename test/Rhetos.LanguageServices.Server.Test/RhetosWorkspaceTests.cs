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
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class RhetosWorkspaceTests
    {
        private readonly ILoggerFactory logFactory;
        private readonly RhetosAppContext rhetosAppContext;

        public RhetosWorkspaceTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            LogManager.Configuration.Reload();
            logFactory = LoggerFactory.Create(b => b.AddConsole());
            rhetosAppContext = new RhetosAppContext(logFactory);
            rhetosAppContext.InitializeFromCurrentDomain();
        }
    }
}
