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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.Dsl.DefaultConcepts;
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
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
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
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
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

        [TestMethod]
        public void CaseInsensitiveParsing()
        {
            var scriptCase = @"
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
            foreach (var script in new [] { scriptCase.ToLower(), scriptCase.ToUpper() })
            {
                Console.WriteLine(script);
                var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
                rhetosDocument.UpdateText(script);
                var analysis = rhetosDocument.GetAnalysis();
                Assert.IsTrue(analysis.SuccessfulRun);
                Console.WriteLine($"Errors: {analysis.AllErrors.Count()}\n\n");
                Assert.AreEqual(0, analysis.AllErrors.Count());
            }
        }

        [DataTestMethod]
        [DataRow(1, 0, null)]
        [DataRow(2, 0, "by Rhetos.Dsl.DefaultConcepts.ModuleInfo")]
        [DataRow(2, 14, "by Rhetos.Dsl.DefaultConcepts.ModuleInfo")]
        [DataRow(2, 15, null)]
        [DataRow(2, 17, null)]
        [DataRow(2, 22, null)]
        [DataRow(3, 0, "by Rhetos.Dsl.DefaultConcepts.ModuleInfo")]
        [DataRow(4, 0, null)]
        [DataRow(4, 21, null)]
        [DataRow(4, 18, "by Rhetos.Dsl.DefaultConcepts.EntityInfo")]
        [DataRow(4, 19, "by Rhetos.Dsl.DefaultConcepts.EntityInfo")]
        [DataRow(5, 0, null)]
        [DataRow(5, 10, "by Rhetos.Dsl.DefaultConcepts.EntityInfo")]
        [DataRow(5, 20, "by Rhetos.Dsl.DefaultConcepts.EntityInfo")]
        [DataRow(6, 0, "by Rhetos.Dsl.DefaultConcepts.EntityInfo")]
        [DataRow(6, 20, null)]
        [DataRow(10, 20, "by Rhetos.Dsl.DefaultConcepts.SimpleReferencePropertyInfo")]
        [DataRow(10, 30, "by Rhetos.Dsl.DefaultConcepts.SimpleReferencePropertyInfo")]
        [DataRow(11, 0, null)]
        [DataRow(12, 8, null)]
        [DataRow(14, 7, null)]
        [DataRow(15, 0, null)]
        public void HoverDocumentation(int line, int chr, string expectedSubstring)
        {
            var script = @"

Module module1 // comment
{
    Entity SameLine {  }
    Entity entity1
    {
        Logging
        {
        }
        Reference name p.   
    }
    Entity Entity2
    {
    }
    error '
}";
            Console.WriteLine(script);
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
            rhetosDocument.UpdateText(script);
            var lineChr = new LineChr(line, chr);
            var positionText = rhetosDocument.TextDocument.ShowPosition(lineChr);
            Console.WriteLine($"\n{positionText}\n");

            var hoverDocumentation = rhetosDocument.GetHoverDescriptionAtPosition(lineChr);
            Console.WriteLine($"Documentation:\n{hoverDocumentation.description}\n");

            if (string.IsNullOrEmpty(expectedSubstring))
            {
                Assert.IsNull(hoverDocumentation.description);
                return;
            }

            StringAssert.Contains(hoverDocumentation.description, expectedSubstring);
        }


        [DataTestMethod]
        
        [DataRow(1, 0, 169, null, null)]
        [DataRow(1, 8, 0, null, null)]
        [DataRow(1, 10, 0, null, null)]
        [DataRow(1, 30, 0, null, null)]
        [DataRow(4, 0, 30, "Entity", "Reference")]
        [DataRow(9, 0, 9, "AllProperties", "Logging")]
        [DataRow(8, 30, 9, "AllProperties", "Logging")]
        [DataRow(11, 0, 66, "Logging", "AllProperties")]
        [DataRow(2, 30, 0, null, null)]
        [DataRow(13, 19, 0, null, null)]
        [DataRow(13, 21, 66, "Logging", "AllProperties")]
        [DataRow(14, 7, 30, "Entity", "Reference")]
        [DataRow(15, 9, 0, null, null)] // shouldn't work after errorline
        [DataRow(16, 30, 0, null, null)]
        [DataRow(17, 0, 0, null, null)]
        [DataRow(17, 30, 0, null, null)]
        public void CompletionContext(int line, int chr, int validKeywordCount, string shouldContainKeyword, string shouldNotContainKeyword)
        {
            var script = @"
        // comment
Module module1
{

    Entity entity1
    {
        Logging
        {

        }

    }
    Entity SameLine {}
    Ent 
    Entit 
    error '
}";
            Console.WriteLine(script);
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
            rhetosDocument.UpdateText(script);
            var lineChr = new LineChr(line, chr);
            var positionText = rhetosDocument.TextDocument.ShowPosition(lineChr);
            Console.WriteLine($"\n{positionText}\n");

            var completion = rhetosDocument.GetCompletionKeywordsAtPosition(lineChr);
            Console.WriteLine($"Keywords: [{completion.Count}] {string.Join(",", completion)}");

            Assert.AreEqual(validKeywordCount, completion.Count);

            if (!string.IsNullOrEmpty(shouldContainKeyword))
            {
                Console.WriteLine($"Should contain: {shouldContainKeyword}");
                CollectionAssert.Contains(completion, shouldContainKeyword);
            }

            if (!string.IsNullOrEmpty(shouldNotContainKeyword))
            {
                Console.WriteLine($"Should NOT contain: {shouldNotContainKeyword}");
                CollectionAssert.DoesNotContain(completion, shouldNotContainKeyword);
            }
        }

        [TestMethod]
        public void CompletionAtEof()
        {
            var script = @"Ent";

            Console.WriteLine(script);
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
            rhetosDocument.UpdateText(script);
            var lineChr = new LineChr(0, 3);
            var positionText = rhetosDocument.TextDocument.ShowPosition(lineChr);
            Console.WriteLine($"\n{positionText}\n");

            var completion = rhetosDocument.GetCompletionKeywordsAtPosition(lineChr);
            Console.WriteLine($"Keywords: [{completion.Count}] {string.Join(",", completion)}");
            Assert.AreEqual(169, completion.Count);
        }

        [TestMethod]
        public void CompletionEmptyFile()
        {
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
            rhetosDocument.UpdateText("");
            var lineChr = new LineChr(0, 3);

            var completion = rhetosDocument.GetCompletionKeywordsAtPosition(lineChr);
            Console.WriteLine($"Keywords: [{completion.Count}] {string.Join(",", completion)}");
            Assert.AreEqual(169, completion.Count);
        }

        [DataTestMethod]
        [DataRow(1, 9, 2, typeof(SimpleReferencePropertyInfo), 0)]
        [DataRow(1, 26, 2, typeof(SimpleReferencePropertyInfo), 1)]
        [DataRow(1, 33, 1, typeof(ReferencePropertyInfo), 2)]
        [DataRow(1, 36, 1, typeof(ReferencePropertyInfo), 2)]
        [DataRow(1, 37, 1, typeof(ReferencePropertyInfo), 2)]
        [DataRow(1, 40, 1, typeof(ReferencePropertyInfo), 2)]
        [DataRow(1, 41, 1, typeof(ReferencePropertyInfo), 2)]
        [DataRow(6, 17, 2, typeof(SimpleReferencePropertyInfo), 1)]
        [DataRow(6, 18, 2, typeof(SimpleReferencePropertyInfo), 1)]
        [DataRow(6, 23, 2, typeof(SimpleReferencePropertyInfo), 1)]
        [DataRow(6, 24, 2, typeof(ReferencePropertyInfo), 2)]
        [DataRow(6, 29, 0, null, 0)]
        [DataRow(6, 35, 0, null, 0)]
        [DataRow(7, 0, 2, typeof(ReferencePropertyInfo), 2)]
        [DataRow(7, 10, 2, typeof(SimpleReferencePropertyInfo), null)] // weird one, but difficult to get around this
        [DataRow(3, 0, 1, typeof(ModuleInfo), 1)]
        [DataRow(4, 0, 0, null, 0)]
        [DataRow(8, 0, 0, null, 0)]
        [DataRow(8, 30, 0, null, 0)]
        public void ActiveSignature(int line, int chr, int totalValidSignatures, Type activeSignatureType, int? activeParameter)
        {
            var script = @"
Reference module1.entity1.name1 refm.refe;
Module module1
{
    Entity entity1
    {
        Reference name1      // comment
    }
    Entity entity2
    {
    }
}";

            Console.WriteLine(script);
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
            rhetosDocument.UpdateText(script);
            var lineChr = new LineChr(line, chr);
            var positionText = rhetosDocument.TextDocument.ShowPosition(lineChr);
            Console.WriteLine($"\n{positionText}\n");

            var signatureHelp = rhetosDocument.GetSignatureHelpAtPosition(lineChr);
            if (totalValidSignatures == 0)
            {
                Console.WriteLine($"SignatureHelp: {signatureHelp}");
                Assert.IsNull(signatureHelp.signatures);
                return;
            }
            
            Assert.IsNotNull(signatureHelp.signatures);
            Console.WriteLine($"Valid signatures: {signatureHelp.signatures.Count}");
            Assert.AreEqual(totalValidSignatures, signatureHelp.signatures.Count);
            Console.WriteLine($"Active signature type: {signatureHelp.signatures[0].ConceptInfoType.Name}");
            Assert.AreEqual(activeSignatureType, signatureHelp.signatures[0].ConceptInfoType);

            if (activeParameter != null) // if activeParameter is null, activeSignature will also be
                Assert.AreEqual(0, signatureHelp.activeSignature);

            Console.WriteLine($"Active parameter: {signatureHelp.activeParameter}");
            Assert.AreEqual(signatureHelp.activeParameter, activeParameter);
        }

        [TestMethod]
        public void SignatureAfterLastParameter()
        {
            var script = "Entity module.entity;\n";

            Console.WriteLine(script);
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
            rhetosDocument.UpdateText(script);

            {
                var lineChr = new LineChr(0, 20);
                var positionText = rhetosDocument.TextDocument.ShowPosition(lineChr);
                Console.WriteLine($"\n{positionText}\n");

                var signatureHelp = rhetosDocument.GetSignatureHelpAtPosition(lineChr);
                Console.WriteLine($"Signature count: {signatureHelp.signatures?.Count}, active parameter: {signatureHelp.activeParameter}");
                Assert.AreEqual(1, signatureHelp.signatures?.Count);
                Assert.AreEqual(2, signatureHelp.activeParameter);
            }
            {
                var lineChr = new LineChr(0, 21);
                var positionText = rhetosDocument.TextDocument.ShowPosition(lineChr);
                Console.WriteLine($"\n{positionText}\n");

                var signatureHelp = rhetosDocument.GetSignatureHelpAtPosition(lineChr);
                Assert.IsNull(signatureHelp.signatures);
            }
        }

        [TestMethod]
        public void SignatureAtEof()
        {
            var script = "Entity module.entit";

            Console.WriteLine(script);
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
            rhetosDocument.UpdateText(script);

            {
                var lineChr = new LineChr(0, 20);
                var positionText = rhetosDocument.TextDocument.ShowPosition(lineChr);
                Console.WriteLine($"\n{positionText}\n");

                var signatureHelp = rhetosDocument.GetSignatureHelpAtPosition(lineChr);
                Console.WriteLine($"Signature count: {signatureHelp.signatures?.Count}, active parameter: {signatureHelp.activeParameter}");
                Assert.AreEqual(1, signatureHelp.signatures?.Count);
                Assert.AreEqual(2, signatureHelp.activeParameter);
            }
        }

        [DataTestMethod]
        [DataRow(14, false)]
        [DataRow(15, true)]
        [DataRow(16, true)]
        [DataRow(25, true)]
        [DataRow(26, true)]
        [DataRow(27, true)]
        [DataRow(28, false)]
        public void QuotedSignatureHelp(int pos, bool expectedIsParam)
        {
            var script = "FilterBy a.b.c 'expression';";

            Console.WriteLine(script);
            var rhetosDocument = rhetosDocumentFactory.CreateWithTestUri();
            rhetosDocument.UpdateText(script);

            var lineChr = new LineChr(0, pos);
            var positionText = rhetosDocument.TextDocument.ShowPosition(lineChr);
            Console.WriteLine($"\n{positionText}\n");

            var signatureHelp = rhetosDocument.GetSignatureHelpAtPosition(lineChr);

            Console.WriteLine(signatureHelp.activeParameter);
            var isParam = signatureHelp.activeParameter == 2;
            Assert.AreEqual(expectedIsParam, isParam);
        }
    }
}
