/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
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

        private readonly string complexScript =
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
    Entity SameLine  {  }
}
Reference a.b.x p ";

        [DataTestMethod]
        [DataRow(0, 0, "Module")]
        [DataRow(0, 1, "Module")]
        [DataRow(0, 10, "Module")]
        [DataRow(1, 0, "Module")]
        [DataRow(1, 1, null)]
        [DataRow(2, 3, null)]
        [DataRow(2, 5, "Entity")]
        [DataRow(4, 8, "Logging")]
        [DataRow(4, 15, "Logging")]
        [DataRow(4, 16, null)]
        [DataRow(5, 18, "Reference")]
        [DataRow(9, 0, null)]
        [DataRow(11, 16, "Entity")]
        [DataRow(11, 17, null)]
        [DataRow(13, 2, null)]
        [DataRow(12, 8, "Entity")]
        [DataRow(12, 18, "Entity")]
        [DataRow(12, 20, "Entity")]
        [DataRow(12, 22, null)]
        [DataRow(14, 0, "Reference")]
        [DataRow(14, 8, "Reference")]
        [DataRow(14, 13, "Reference")]
        [DataRow(14, 20, "Reference")]
        public void CorrectKeywords(int line, int chr, string expectedKeyword)
        {
            var lineChr = new LineChr(line, chr);
            var rhe = rhetosDocumentFactory.CreateWithTestUri(complexScript, lineChr);

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
        [DataRow(12, 22, "TestModule / TestModule.SameLine")]
        [DataRow(14, 0, "")]
        public void CorrectConceptContexts(int line, int chr, string expectedContext)
        {
            var lineChr = new LineChr(line, chr);
            var rhe = rhetosDocumentFactory.CreateWithTestUri(complexScript, lineChr);

            var analysis = rhe.GetAnalysis(lineChr);
            Console.WriteLine(analysis.TextDocument.ShowPosition(lineChr));
            var context = analysis.ConceptContext;
            var contextDesc = string.Join(" / ", context);
            Console.WriteLine($"Context at cursor ({line}, {chr}): {contextDesc}");
            Assert.AreEqual(expectedContext, contextDesc);
        }

        [DataTestMethod]
        [DataRow(2, 0, "M")]
        [DataRow(4, 0, "M / M.E")]
        [DataRow(4, 5, "M")]
        [DataRow(5, 4, "M")]
        public void CorrectParsingWithErrors(int line, int chr, string expectedContext)
        {
            var scriptErrors =
@"Module M
{ 
    Entity E
    {
    }
    error
}
";

            var lineChr = new LineChr(line, chr);
            var rhe = rhetosDocumentFactory.CreateWithTestUri(scriptErrors, lineChr);

            var analysisResult = rhe.GetAnalysis(lineChr);
            Assert.IsTrue(analysisResult.SuccessfulRun);

            Assert.IsTrue(analysisResult.AllErrors.Any());
            Console.WriteLine(string.Join("\n", analysisResult.AllErrors));

            var contextDesc = string.Join(" / ", analysisResult.ConceptContext);
            Console.WriteLine($"Context at cursor ({line}, {chr}): {contextDesc}");
            Assert.AreEqual(expectedContext, contextDesc);
        }

        [TestMethod]
        public void SameLineVsNewLineBraces()
        {
            foreach (var data in new []
            {
                ("Module module1 {   }", new LineChr(0, 17)), 
                ("Module module2\n{\n\n}\n", new LineChr(2, 0))
            })
            {
                var rhe = rhetosDocumentFactory.CreateWithTestUri(data.Item1, data.Item2);

                var analysisResult = rhe.GetAnalysis(data.Item2);
                for (var i = 0; i < analysisResult.Tokens.Count; i++)
                {
                    Console.WriteLine($"[{i}] ({analysisResult.Tokens[i].Type}): '{analysisResult.Tokens[i].Value}'");
                }
                Console.WriteLine($"Keyword at pos: '{analysisResult.KeywordToken?.Value}'\n\n");
                Assert.IsNull(analysisResult.KeywordToken);
            }
        }

        [TestMethod]
        public void HandleTokenError()
        {
            var scriptTokenError =
@"Module 'M
{ 
    Entity E
    {
    }
}
";
            var rhe = rhetosDocumentFactory.CreateWithTestUri(scriptTokenError);
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
                var document = documentFactory.CreateWithTestUri();
                var analysisResult = document.GetAnalysis();
                Assert.AreEqual(1, analysisResult.AllErrors.Count());
                Assert.IsFalse(analysisResult.SuccessfulRun);
                Console.WriteLine(analysisResult.AllErrors.First().Message);
                StringAssert.Contains(analysisResult.AllErrors.First().Message, "No valid RhetosProjectRootPath configuration was found");
            }
        }

        [DataTestMethod]
        [DataRow(0, 14, "Module", false)]
        [DataRow(0, 15, null, true)]
        [DataRow(0, 25, null, true)]
        [DataRow(0, 26, null, true)]
        [DataRow(0, 27, null, true)]
        [DataRow(1, 0, "Module", false)]
        [DataRow(1, 1, null, false)]
        [DataRow(2, 3, null, false)]
        [DataRow(2, 4, "Entity", false)]
        [DataRow(2, 10, "Entity", false)]
        [DataRow(3, 0, null, true)]
        [DataRow(3, 15, null, true)]
        [DataRow(3, 20, null, true)]
        [DataRow(4, 0, "Entity", false)]
        [DataRow(4, 3, "Entity", false)]
        [DataRow(4, 4, "Entity", false)]
        [DataRow(6, 10, null, true)]
        public void CorrectKeywordsWithComments(int line, int chr, string expectedKeyword, bool isInsideComment)
        {
            var commentScript =
@"Module module1 // comment 1
{
    Entity entity1
// comment 2
    {
    }
// s
}";

            var rhe = rhetosDocumentFactory.CreateWithTestUri(commentScript);
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
            var rhe = rhetosDocumentFactory.CreateWithTestUri(script + scriptPostfix);

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
                var conceptType = pair.conceptType;
                Console.WriteLine($"{conceptType.Name} ==> {ConceptInfoType.SignatureDescription(conceptType)}");
                var members = ConceptInfoType.GetParameters(pair.conceptType);
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
        public void ConceptActiveParameterEof()
        {
            var lineChr = new LineChr(0, 23);
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri("Module module1; Module ", lineChr);
            var analysis = rhetosDocument.GetAnalysis(lineChr);

            var validConcepts = analysis.GetValidConceptsWithActiveParameter();
            Assert.AreEqual(1, validConcepts.Count);
            Assert.AreEqual(0, validConcepts.First().activeParamater);
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
            var rhe = rhetosDocumentFactory.CreateWithTestUri(script);

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

        [DataTestMethod]
        [DataRow("token", 0, 0, null)]
        [DataRow("token", 0, 4, null)]
        [DataRow("token", 0, 5, "token")]
        [DataRow("token", 0, 6, null)] // this is ambiguous, but we will keep convention that it is null
        [DataRow("token\na", 1, 0, null)]
        [DataRow("token\na", 1, 1, "a")]
        [DataRow("token\na", 1, 2, null)]
        public void TokenBeingTyped(string script, int line, int chr, string expectedToken)
        {
            var lineChr = new LineChr(line, chr);
            var rhe = rhetosDocumentFactory.CreateWithTestUri(script, lineChr);

            var analysisResult = rhe.GetAnalysis(new LineChr(line, chr));
            var tokenTyped = analysisResult.GetTokenBeingTypedAtCursor(new LineChr(line, chr));
            Assert.AreEqual(expectedToken, tokenTyped?.Value);
        }

        [DataTestMethod]
        [DataRow("token", 0, 4, "token")]
        [DataRow("token", 0, 5, null)]
        [DataRow("token", 0, 6, null)]
        [DataRow("token\n", 0, 5, null)]
        public void TokenAtPosition(string script, int line, int chr, string expectedToken)
        {
            var lineChr = new LineChr(line, chr);
            var rhe = rhetosDocumentFactory.CreateWithTestUri(script, lineChr);

            var analysisResult = rhe.GetAnalysis(new LineChr(line, chr));
            var token = analysisResult.GetTokenAtPosition(new LineChr(line, chr));
            Assert.AreEqual(expectedToken, token?.Value);
        }
    }
}
