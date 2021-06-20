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
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.CommonTestTools;

namespace Rhetos.LanguageServices.CodeAnalysis.Test
{
    [TestClass]
    [DeploymentItem("DslSyntax.json")]
    [DeploymentItem("DslDocumentation.json")]
    public class ConceptQueriesTests
    {
        private readonly IServiceProvider serviceProvider;

        public ConceptQueriesTests()
        {
            serviceProvider = TestCommon.CreateTestServiceProvider("./");
        }

        // Derived ConceptInfo classes without own keywords should not be included
        [TestMethod]
        public void DerivedInfoWithoutKeyword()
        {
            var conceptQueries = serviceProvider.GetRequiredService<ConceptQueries>();
            var signatures = conceptQueries.GetSignaturesWithDocumentation("SqlObject");
            Assert.AreEqual(1, signatures.Count);
            StringAssert.StartsWith(signatures.Single().Signature, "SqlObject");
        }

        [TestMethod]
        public void ValidForParent()
        {
            var conceptQueries = serviceProvider.GetRequiredService<ConceptQueries>();
            var rhetosProjectContext = serviceProvider.GetRequiredService<RhetosProjectContext>();

            var browseInfoType = rhetosProjectContext.Keywords["Browse"].First();
            var moduleInfoType = rhetosProjectContext.Keywords["Module"].First();
            var entityInfoType = rhetosProjectContext.Keywords["Entity"].First();
            var shortStringInfoType = rhetosProjectContext.Keywords["ShortString"].First();

            {
                var validTypes = conceptQueries.ValidConceptsForParent(moduleInfoType);
                CollectionAssert.Contains(validTypes, browseInfoType);
                CollectionAssert.Contains(validTypes, entityInfoType);
            }

            // Browse inside Entity has Module parameter fulfilled, but shouldn't be valid because Entity is an extra parent it doesn't require as a key
            {
                var validTypes = conceptQueries.ValidConceptsForParent(entityInfoType);
                CollectionAssert.DoesNotContain(validTypes, browseInfoType);
                CollectionAssert.DoesNotContain(validTypes, entityInfoType);
                CollectionAssert.DoesNotContain(validTypes, moduleInfoType);
            }

            // Browse can't contain Browse, Entity or Module
            {
                var validTypes = conceptQueries.ValidConceptsForParent(browseInfoType);
                CollectionAssert.DoesNotContain(validTypes, browseInfoType);
                CollectionAssert.DoesNotContain(validTypes, entityInfoType);
                CollectionAssert.DoesNotContain(validTypes, moduleInfoType);
            }

            // Support parent from derived key classes
            {
                var validTypes = conceptQueries.ValidConceptsForParent(entityInfoType);
                //Console.WriteLine(JsonConvert.SerializeObject(shortStringInfoType, Formatting.Indented));
                //Console.WriteLine(JsonConvert.SerializeObject(entityInfoType, Formatting.Indented));
                CollectionAssert.Contains(validTypes, shortStringInfoType);
            }
        }

        [TestMethod]
        public void RhetosSignatureEntity()
        {
            var conceptQueries = serviceProvider.GetRequiredService<ConceptQueries>();
            var signatures = conceptQueries.GetSignaturesWithDocumentation("Entity");
            Console.WriteLine(JsonConvert.SerializeObject(signatures, Formatting.Indented));

            Assert.AreEqual(1, signatures.Count);
            var signature = signatures.Single();
            StringAssert.Contains(signature.Documentation, "* defined by Rhetos.Dsl.DefaultConcepts.EntityInfo");
            Assert.AreEqual("Entity <Module: ModuleInfo>.<Name: String> ", signature.Signature);
            Assert.AreEqual(signature.ConceptType.TypeName, "EntityInfo");
            Assert.AreEqual("Module: ModuleInfo,Name: String", string.Join(",", signature.Parameters.Select(ConceptTypeTools.ConceptMemberDescription)));
        }

        [TestMethod]
        public void RhetosSignatureReference()
        {
            var conceptQueries = serviceProvider.GetRequiredService<ConceptQueries>();
            var signatures = conceptQueries.GetSignaturesWithDocumentation("Reference");
            Console.WriteLine(JsonConvert.SerializeObject(signatures, Formatting.Indented));

            Assert.AreEqual(2, signatures.Count);
            var signature = signatures.Single(sig => sig.ConceptType.TypeName == "ReferencePropertyInfo");
            StringAssert.Contains(signature.Documentation, "* defined by Rhetos.Dsl.DefaultConcepts.ReferencePropertyInfo");
            Assert.AreEqual("Reference <DataStructure: DataStructureInfo>.<Name: String> <Referenced: DataStructureInfo>", signature.Signature);
            Assert.AreEqual("DataStructure: DataStructureInfo,Name: String,Referenced: DataStructureInfo", string.Join(",", signature.Parameters.Select(ConceptTypeTools.ConceptMemberDescription)));
        }

        [DataTestMethod]
        [DataRow("referencE", 2, "ReferencePropertyInfo")]
        [DataRow("entiTy", 1, "EntityInfo")]
        [DataRow("allproPerties", 4, "AllProperties <EntityComputedFrom: EntityComputedFromInfo> ")]
        public void QueriesAreCaseInsensitive(string invariantKeyword, int expectedSignatureCount, string expectedDescriptionSubstring)
        {
            var variants = new[]
            {
                invariantKeyword,
                invariantKeyword.ToLower(),
                invariantKeyword.ToUpper(),
                CultureInfo.InvariantCulture.TextInfo.ToTitleCase(invariantKeyword)
            };

            var conceptQueries = serviceProvider.GetRequiredService<ConceptQueries>();

            foreach (var keyword in variants)
            {
                var fullDescription = conceptQueries.GetFullDescription(keyword);
                Console.WriteLine($"\nKeyword variant: '{keyword}'\n\nFull description:\n{fullDescription}\n");

                var signatures = conceptQueries.GetSignaturesWithDocumentation(keyword);
                Assert.IsNotNull(signatures);
                Console.WriteLine($"Signature count: {signatures.Count}.");
                Assert.AreEqual(expectedSignatureCount, signatures.Count);

                StringAssert.Contains(fullDescription, expectedDescriptionSubstring);
            }
        }

        [DataTestMethod]
        [DataRow("Entity", "Represents data entered by a user or generated by the system", "Remarks")]
        [DataRow("Module", "Generates a C# namespace and a database schema", "Remarks")]
        [DataRow("AllProperties", "Enables logging on all properties", "Remarks")]
        [DataRow("AllProperties", "Includes all properties in temporal data management", "Remarks")]
        [DataRow("RepositoryUses", "Adds the private C# property to the data structure's repository class", "")]
        [DataRow("RepositoryUses", "Remarks:", "")]
        [DataRow("RepositoryUses", "the property value should be the assembly qualified name", "")]
        public void XmlDocumentationCorrect(string keyword, string contains, string doesNotContain)
        {
            var conceptQueries = serviceProvider.GetRequiredService<ConceptQueries>();
            var doc = conceptQueries.GetFullDescription(keyword);
            Console.WriteLine(doc);

            if (!string.IsNullOrEmpty(contains))
                Assert.IsTrue(doc.Contains(contains));

            if (!string.IsNullOrEmpty(doesNotContain))
                Assert.IsFalse(doc.Contains(doesNotContain));
        }
    }
}
