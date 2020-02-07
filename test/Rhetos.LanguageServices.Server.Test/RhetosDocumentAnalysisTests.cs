using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class RhetosDocumentAnalysisTests
    {
        private readonly ILoggerFactory logFactory;
        private readonly RhetosAppContext rhetosAppContext;

        public RhetosDocumentAnalysisTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            logFactory = LoggerFactory.Create(b => b.AddConsole());
            rhetosAppContext = new RhetosAppContext(logFactory);
            rhetosAppContext.InitializeFromCurrentDomain();
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
        [DataRow(12, 2, null)]
        public void CorrectKeywords(int line, int chr, string expectedKeyword)
        {
            Console.WriteLine(script);
            var rhe = new RhetosDocument(rhetosAppContext, logFactory);
            rhe.UpdateText(script);

            Console.WriteLine(rhe.TextDocument.ShowPosition(line, chr));
            var keywordToken = rhe.GetAnalysis(line, chr).KeywordToken;
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
            var rhe = new RhetosDocument(rhetosAppContext, logFactory);
            rhe.UpdateText(script);

            Console.WriteLine(rhe.TextDocument.ShowPosition(line, chr));
            var context = rhe.GetAnalysis(line, chr).ConceptContext;
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
            var rhe = new RhetosDocument(rhetosAppContext, logFactory);
            rhe.UpdateText(scriptErrors);

            Console.WriteLine(rhe.TextDocument.ShowPosition(line, chr));
            var analysisResult = rhe.GetAnalysis(line, chr);

            Assert.IsTrue(analysisResult.Errors.Any());
            var error = analysisResult.Errors.First();
            Console.WriteLine($"ERROR: {error.Message}.");
            StringAssert.Contains(error.Message, "Unrecognized concept keyword 'error'.");
            Assert.AreEqual(5, error.Line);
            Assert.AreEqual(4, error.Chr);
            var contextDesc = string.Join(" / ", analysisResult.ConceptContext);
            Console.WriteLine($"Context at cursor ({line}, {chr}): {contextDesc}");
            Assert.AreEqual(expectedContext, contextDesc);
        }

    }
}
