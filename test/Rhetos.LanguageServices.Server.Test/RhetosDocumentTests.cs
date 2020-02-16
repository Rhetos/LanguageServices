using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class RhetosDocumentTests
    {
        private readonly IServiceProvider serviceProvider;
        private readonly RhetosDocumentFactory rhetosDocumentFactory;

        public RhetosDocumentTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            serviceProvider = TestCommon.CreateTestServiceProvider();
            serviceProvider.GetService<RhetosAppContext>().InitializeFromCurrentDomain();
            rhetosDocumentFactory = serviceProvider.GetService<RhetosDocumentFactory>();
        }

        private readonly string scriptSimple = @"
Module module1
{
    Entity entity1
    {
        ShortString string1;
        ShortString string2;
        ShortString string3;
    }
}
";

        [TestMethod]
        public void AnalysisCache()
        {
            var rhetosDocument = rhetosDocumentFactory.CreateNew();
            rhetosDocument.UpdateText(scriptSimple);
            var halfPos = rhetosDocument.TextDocument.GetLineChr(scriptSimple.Length / 2); // force errors

            var result = rhetosDocument.GetAnalysis(halfPos);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.SuccessfulRun);

            var resultCached = rhetosDocument.GetAnalysis(halfPos);
            Assert.AreEqual(result, resultCached);
            Console.WriteLine($"Second run was cached result.");

            rhetosDocument.UpdateText(scriptSimple + scriptSimple);
            var resultNoCache = rhetosDocument.GetAnalysis(halfPos);
            Assert.AreNotEqual(result, resultNoCache);
            Console.WriteLine($"After text update, cache was invalidated.");
        }

        [TestMethod]
        public void AnalysisPerformance()
        {
            var scriptRepeats = 1000;
            Console.WriteLine(scriptSimple);
            var script = string.Join("\n", Enumerable.Range(0, scriptRepeats).Select(_ => scriptSimple));
            Console.WriteLine($"Total script length: {script.Length}.");
            var rhetosDocument = rhetosDocumentFactory.CreateNew();
            rhetosDocument.UpdateText(script);
            var halfPos = rhetosDocument.TextDocument.GetLineChr(script.Length / 2 + scriptSimple.Length / 2); // force errors

            foreach (var lineChr in new [] { (LineChr?)null, halfPos })
            {
                var sw = Stopwatch.StartNew();
                var result = rhetosDocument.GetAnalysis(lineChr);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.SuccessfulRun);
                Console.WriteLine($"Analysis run: {sw.Elapsed.TotalMilliseconds:0.00} ms. Position: '{lineChr}'. Errors: {result.AllErrors.Count()}");
            }
        }
/*
        [TestMethod]
        public void Test()
        {
            var text = @"
Module module1
{
    Entity entity1
    {
        Reference 
    }
}
";
            var rhetosDocument = rhetosDocumentFactory.CreateNew();
            rhetosDocument.UpdateText(text);

            var lineChr = new LineChr(5, 30);
            Console.WriteLine(text);
            Console.WriteLine(rhetosDocument.TextDocument.ShowPosition(lineChr));

            var signatureHelp = rhetosDocument.GetSignatureHelpAtPosition(lineChr);
            Console.WriteLine(JsonConvert.SerializeObject(signatureHelp, Formatting.Indented));
        }*/
    }
}
