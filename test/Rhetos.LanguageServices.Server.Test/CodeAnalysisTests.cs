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
            var analysis = rhe.GetAnalysis(lineChr);
            Console.WriteLine(analysis.TextDocument.ShowPosition(lineChr));
            var context = analysis.ConceptContext;
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
        [DataRow(5, 4, "M")]
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
            Console.WriteLine(string.Join("\n", analysisResult.AllErrors));
            
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

        private readonly string commentScript =
@"Module module1 // comment 1
{
    Entity entity1
// comment 2
    {
    }
// s
}";
        [DataTestMethod]
        [DataRow(0, 14, "Module", false)]
        [DataRow(0, 15, null, true)]
        [DataRow(0, 25, null, true)]
        [DataRow(0, 26, null, true)]
        [DataRow(0, 27, null, true)]
        [DataRow(1, 0, null, false)]
        [DataRow(1, 1, null, false)]
        [DataRow(2, 3, null, false)]
        [DataRow(2, 4, "Entity", false)]
        [DataRow(2, 10, "Entity", false)]
        [DataRow(3, 0, null, true)]
        [DataRow(3, 15, null, true)]
        [DataRow(3, 20, null, true)]
        [DataRow(4, 0, "Entity", false)]
        [DataRow(4, 3, "Entity", false)]
        [DataRow(4, 4, null, false)]
        [DataRow(6, 10, null, true)]
        public void CorrectKeywordsWithComments(int line, int chr, string expectedKeyword, bool isInsideComment)
        {
            var rhe = rhetosDocumentFactory.CreateNew();
            rhe.UpdateText(commentScript);
            Console.WriteLine(commentScript);
            Console.WriteLine();

            var lineChr = new LineChr(line, chr);
            var analysisResult = rhe.GetAnalysis(lineChr);

            Console.WriteLine($"Found comment tokens:\n-----------------------");
            foreach (var commentToken in analysisResult.CommentTokens)
            {
                var commentLineChr = analysisResult.TextDocument.GetLineChr(commentToken.PositionInDslScript);
                Console.WriteLine($"Comment Token: '{commentToken.Value}' at position {commentLineChr}");
                Console.WriteLine(analysisResult.TextDocument.ShowPosition(commentLineChr));
            }

            Console.WriteLine($"Keywords:\n-----------------------");
            Console.WriteLine($"Keyword at position: {analysisResult.KeywordToken?.Value}");
            Console.WriteLine(rhe.TextDocument.ShowPosition(lineChr));

            Assert.AreEqual(expectedKeyword, analysisResult.KeywordToken?.Value);
            Assert.AreEqual(isInsideComment, analysisResult.IsInsideComment);
        }
    }
}
