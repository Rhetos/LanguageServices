using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Rhetos.Dsl;
using Rhetos.Dsl.DefaultConcepts;
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
Reference a.b.x p ";

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
        [DataRow(13, 0, "Reference")]
        [DataRow(13, 8, "Reference")]
        [DataRow(13, 13, "Reference")]
        [DataRow(13, 20, "Reference")]
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

        [DataTestMethod]
        [DataRow("Reference", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference  ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference  ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "post")]
        [DataRow("Reference  ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "post.x.y")]
        [DataRow("Reference module", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference module ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference module  ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference module  ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "post")]
        [DataRow("Reference module  ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "post.x.y")]
        [DataRow("Reference module.", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference module. ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference module.  ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference module.  ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "post")]
        [DataRow("Reference module.  ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "post.x.y")]
        [DataRow("Reference module.entity", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference module.entity ", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference module.entity  ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "")]
        [DataRow("Reference module.entity  ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "post")]
        [DataRow("Reference module.entity  ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "post.x.y")]
        [DataRow("Reference module.entity.", "SimpleReferencePropertyInfo:DataStructure,ReferencePropertyInfo:DataStructure", "")]
        [DataRow("Reference module.entity. ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "")]
        [DataRow("Reference module.entity.  ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "")]
        [DataRow("Reference module.entity.  ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "post")]
        [DataRow("Reference module.entity.  ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "post.x.y")]
        [DataRow("Reference module.entity.name", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "")]
        [DataRow("Reference module.entity.name ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "")]
        [DataRow("Reference module.entity.name  ", "SimpleReferencePropertyInfo:FULL,ReferencePropertyInfo:Referenced", "")]
        [DataRow("Reference module.entity.name  ", "SimpleReferencePropertyInfo:FULL,ReferencePropertyInfo:Referenced", "post")]
        [DataRow("Reference module.entity.name  ", "SimpleReferencePropertyInfo:FULL,ReferencePropertyInfo:Referenced", "post.x.y")]
        [DataRow("Reference module.entity.name ref", "ReferencePropertyInfo:Referenced", "")]
        [DataRow("Reference module.entity.name ref ", "ReferencePropertyInfo:Referenced", "")]
        [DataRow("Reference module.entity.name ref  ", "ReferencePropertyInfo:Referenced", "post")]
        [DataRow("Reference module.entity.name ref.", "ReferencePropertyInfo:Referenced", "")]
        [DataRow("Reference module.entity.name ref. ", "ReferencePropertyInfo:Referenced", "")]
        [DataRow("Reference module.entity.name ref.  ", "ReferencePropertyInfo:Referenced", "")]
        [DataRow("Reference module.entity.name ref.  ", "ReferencePropertyInfo:Referenced", "post")]
        [DataRow("Reference module.entity.name ref.refname", "ReferencePropertyInfo:Referenced", "")]
        [DataRow("Reference module.entity.name ref.refname ", "ReferencePropertyInfo:Referenced", "")]
        [DataRow("Reference module.entity.name ref.refname  ", "ReferencePropertyInfo:FULL", "")]
        [DataRow("Reference module.entity.name ref.refname  ", "ReferencePropertyInfo:FULL", "post")]
        [DataRow("Reference module.entity.name ref.refname extraparam", "", "")]
        [DataRow("Module module1 { Entity entity1 { Reference", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "")]
        [DataRow("Module module1 { Entity entity1 { Reference ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "")]
        [DataRow("Module module1 { Entity entity1 { Reference  ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "")]
        [DataRow("Module module1 { Entity entity1 { Reference  ", "SimpleReferencePropertyInfo:Name,ReferencePropertyInfo:Name", "post")]
        public void ConceptActiveParameter(string script, string expectedActiveParams, string scriptPostfix)
        {
            var rhe = rhetosDocumentFactory.CreateNew();
            rhe.UpdateText(script + scriptPostfix);
            Console.WriteLine($"Script:\n{rhe.TextDocument.Text}\n");
            var conceptQueries = serviceProvider.GetService<ConceptQueries>();
            Console.WriteLine(conceptQueries.GetFullDescription("Reference"));
            Console.WriteLine();

            var lineChr = rhe.TextDocument.GetLineChr(script.Length - 1);
            Console.WriteLine(rhe.TextDocument.ShowPosition(lineChr));
            var analysis = rhe.GetAnalysis(lineChr);

            var withParam = analysis.GetValidConceptsWithActiveParameter();
            var formattedParams = new List<string>();
            Console.WriteLine("\n");
            foreach (var pair in withParam)
            {
                var conceptType = pair.concept.GetType();
                Console.WriteLine($"{conceptType.Name} ==> {ConceptInfoType.SignatureDescription(conceptType)}");
                //Console.WriteLine($"Last member read: {analysis.LastMemberReadAttempt[conceptType].Name}");
                var members = ConceptInfoType.GetParameters(pair.concept.GetType());
                var memberName = pair.activeParamater < members.Count ? members[pair.activeParamater].Name : "FULL";
                var formatted = $"{conceptType.Name}:{memberName}";
                formattedParams.Add(formatted);
            }

            var activeParams = string.Join(",", formattedParams);
            Console.WriteLine();
            Console.WriteLine(activeParams);
            Assert.AreEqual(expectedActiveParams, activeParams);
        }

        [TestMethod]
        public void ConceptSignatureHelpSortOrder()
        {
            var script = @"// <rhetosRootPath=""c:\SomeFolder"" />

Reference a.b.xpa     
Module sasa
{
	Entity pero
	{
	}
}
";
            var rhe = rhetosDocumentFactory.CreateNew();
            rhe.UpdateText(script);
            Console.WriteLine($"Script:\n{rhe.TextDocument.Text}\n");
            var conceptQueries = serviceProvider.GetService<ConceptQueries>();
            Console.WriteLine(conceptQueries.GetFullDescription("Reference"));
            Console.WriteLine();

            {
                var lineChr = new LineChr(2, 20);
                Console.WriteLine(rhe.TextDocument.ShowPosition(lineChr));
                var signatureHelp = rhe.GetSignatureHelpAtPosition(lineChr);
                Console.WriteLine($"Chosen signature: {signatureHelp.signatures[0].Signature}\nActive parameter: {signatureHelp.activeParameter}\n");
                Assert.AreEqual(typeof(ReferencePropertyInfo), signatureHelp.signatures[0].ConceptInfoType);
                Assert.AreEqual(2, signatureHelp.activeParameter);
            }

            {
                var lineChr = new LineChr(2, 17);
                Console.WriteLine(rhe.TextDocument.ShowPosition(lineChr));
                var signatureHelp = rhe.GetSignatureHelpAtPosition(lineChr);
                Console.WriteLine($"Chosen signature: {signatureHelp.signatures[0].Signature}\nActive parameter: {signatureHelp.activeParameter}\n");
                Assert.AreEqual(typeof(SimpleReferencePropertyInfo), signatureHelp.signatures[0].ConceptInfoType);
                Assert.AreEqual(1, signatureHelp.activeParameter);
            }
        }
    }
}
