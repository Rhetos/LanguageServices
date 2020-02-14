using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class CodeAnalysisTests
    {
        private readonly IServiceProvider serviceProvider;
        private readonly RhetosDocumentFactory rhetosDocumentFactory;

        public CodeAnalysisTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            serviceProvider = TestCommon.CreateTestServiceProvider();

            serviceProvider.GetService<RhetosAppContext>().InitializeFromCurrentDomain();
            rhetosDocumentFactory = serviceProvider.GetService<RhetosDocumentFactory>();
        }

        private readonly string script =
@"Module TestModule
{ 
    Entity Pero
    {
        Logging;
        Reference blo;
    }
    Entity Empty
    { 
 
    }
    Entity After; 
}
";

        [DataTestMethod]
        [DataRow(0, 0, "Module")]
        [DataRow(0, 1, "Module")]
        [DataRow(0, 10, "Module")]
        [DataRow(1, 0, null)]
        [DataRow(2, 3, null)]
        [DataRow(2, 5, "Entity")]
        [DataRow(4, 8, "Logging")]
        [DataRow(4, 15, null)]
        [DataRow(5, 18, "Reference")]
        [DataRow(9, 0, null)]
        [DataRow(11, 16, null)]
        [DataRow(12, 2, null)]
        public void CorrectKeywords(int line, int chr, string expectedKeyword)
        {
            Console.WriteLine(script);
            var lineChr = new LineChr(line, chr);
            var rhe = rhetosDocumentFactory.CreateNew();
            rhe.UpdateText(script);

            Console.WriteLine(rhe.TextDocument.ShowPosition(lineChr));
            var keywordToken = rhe.GetAnalysis(lineChr).KeywordToken;
            var keyword = keywordToken?.Value;
            Console.WriteLine($"Keyword at cursor ({line}, {chr}): {keyword}");
            Assert.AreEqual(expectedKeyword, keyword);
        }

        [DataTestMethod]
        [DataRow(0, 0, "")]
        [DataRow(0, 10, "")]
        [DataRow(1, 0, "")]
        [DataRow(1, 1, "TestModule")]
        [DataRow(3, 0, "TestModule")]
        [DataRow(3, 4, "TestModule")]
        [DataRow(3, 5, "TestModule / TestModule.Pero")]
        [DataRow(6, 4, "TestModule / TestModule.Pero")]
        [DataRow(6, 5, "TestModule")]
        [DataRow(9, 0, "TestModule / TestModule.Empty")]
        [DataRow(13, 0, "")]
        public void CorrectConceptContexts(int line, int chr, string expectedContext)
        {
            Console.WriteLine(script);
            var lineChr = new LineChr(line, chr);
            var rhe = rhetosDocumentFactory.CreateNew();
            rhe.UpdateText(script);

            Console.WriteLine(rhe.TextDocument.ShowPosition(lineChr));
            var context = rhe.GetAnalysis(lineChr).ConceptContext;
            var contextDesc = string.Join(" / ", context);
            Console.WriteLine($"Context at cursor ({line}, {chr}): {contextDesc}");
            Assert.AreEqual(expectedContext, contextDesc);
        }

        private readonly string scriptErrors =
@"Module M
{ 
    Entity E
    {
    }
    error
}
";
        [DataTestMethod]
        [DataRow(2, 0, "M")]
        [DataRow(4, 0, "M / M.E")]
        [DataRow(4, 5, "M")]
        public void CorrectParsingWithErrors(int line, int chr, string expectedContext)
        {
            Console.WriteLine(scriptErrors);
            var lineChr = new LineChr(line, chr);
            var rhe = rhetosDocumentFactory.CreateNew();
            rhe.UpdateText(scriptErrors);

            Console.WriteLine(rhe.TextDocument.ShowPosition(lineChr));
            var analysisResult = rhe.GetAnalysis(lineChr);
            Assert.IsTrue(analysisResult.SuccessfulRun);

            Assert.IsTrue(analysisResult.AllErrors.Any());
            var error = analysisResult.AllErrors.First();
            Console.WriteLine($"ERROR: {error.Message}.");
            StringAssert.Contains(error.Message, "Unrecognized concept keyword 'error'.");
            Assert.AreEqual(5, error.LineChr.Line);
            Assert.AreEqual(4, error.LineChr.Chr);
            var contextDesc = string.Join(" / ", analysisResult.ConceptContext);
            Console.WriteLine($"Context at cursor ({line}, {chr}): {contextDesc}");
            Assert.AreEqual(expectedContext, contextDesc);
        }

        private readonly string scriptTokenError =
@"Module 'M
{ 
    Entity E
    {
    }
}
";

        [TestMethod]
        public void HandleTokenError()
        {
            Console.WriteLine(scriptTokenError);
            var rhe = rhetosDocumentFactory.CreateNew();
            rhe.UpdateText(scriptTokenError);
            var analysisResult = rhe.GetAnalysis();
            Assert.AreEqual(1, analysisResult.TokenizerErrors.Count);
            Console.WriteLine(JsonConvert.SerializeObject(analysisResult.TokenizerErrors[0], Formatting.Indented));
            StringAssert.Contains(analysisResult.TokenizerErrors[0].Message, "Missing closing character");
        }

        [TestMethod]
        public void RunAnalysisOnUninitilizedRhetosAppContext()
        {
            var newProvider = TestCommon.CreateTestServiceProvider();

            var documentFactory = newProvider.GetService<RhetosDocumentFactory>();

            {
                var document = documentFactory.CreateNew();
                var analysisResult = document.GetAnalysis();
                Assert.AreEqual(0, analysisResult.AllErrors.Count());
                Assert.IsFalse(analysisResult.SuccessfulRun);
            }
        }
    }
}
