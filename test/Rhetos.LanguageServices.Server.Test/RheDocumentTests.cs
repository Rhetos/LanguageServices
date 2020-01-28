using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class RheDocumentTests
    {
        private readonly RhetosAppContext rhetosAppContext;

        public RheDocumentTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            rhetosAppContext = new RhetosAppContext(loggerFactory.CreateLogger<RhetosAppContext>());
            rhetosAppContext.InitializeFromCurrentDomain();
        }

        [TestMethod]
        public void ParsesTokens()
        {
//            var text = "entity 'sasa stublic'\r\n{\r\n\tperas;\r\n}\r\nas";
            var text =
@"entity 'sasa stublic'
{
	peras;
}
as";
            var logProvider = new NLogProvider();
            var rhe = new RheDocument(text, rhetosAppContext, logProvider);
            var tokens = rhe.Tokenizer.GetTokens();
            Console.WriteLine(tokens.Count);
            //Console.WriteLine(JsonConvert.SerializeObject(tokens, Formatting.Indented));
            var tokenAt = rhe.GetTokenAtPosition(0, 5);
            Console.WriteLine($"At pos: {tokenAt?.Value}");
        }

    }

}
