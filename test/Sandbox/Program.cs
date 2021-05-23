using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.Logging;
using Rhetos.Utilities;
using Sandbox.Parsing;

namespace Sandbox
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var scriptProvider = new DslScriptsProvider("Module ble {Entity Pero\n{ShortString ble;\n}}");
            var logProvider = new ConsoleLoggerProvider();

            var serializerSettings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
            };

            var dslSyntax = JsonConvert.DeserializeObject<DslSyntax>(File.ReadAllText("DslSyntax.json"), serializerSettings);


            var run = new CodeAnalysisRun(new TextDocument("Module ble {Entity Pero\n{ShortString ble;\n}}", new Uri("c:/")),
                new RhetosProjectContext(dslSyntax),
                LoggerFactory.Create(a => a.AddConsole())
            );

            var result = run.RunForDocument();

            Console.WriteLine(JsonConvert.SerializeObject(result.AllErrors));

            return;
            var tokenizer = new Tokenizer(scriptProvider, new FilesUtility(logProvider), dslSyntax);
            var dslParser = new DslParser(tokenizer, dslSyntax, logProvider);

            var concepts = dslParser.ParsedConcepts;
        }
    }
}
