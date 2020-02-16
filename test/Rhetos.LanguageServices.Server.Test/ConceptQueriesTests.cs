using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.Dsl;
using Rhetos.Dsl.DefaultConcepts;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class ConceptQueriesTests
    {
        private readonly IServiceProvider serviceProvider;

        public ConceptQueriesTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            serviceProvider = TestCommon.CreateTestServiceProvider();
            serviceProvider.GetService<RhetosAppContext>().InitializeFromCurrentDomain();
        }

        // Derived ConceptInfo classes without own keywords should not be included
        [TestMethod]
        public void DerivedInfoWithoutKeyword()
        {
            var conceptQueries = serviceProvider.GetService<ConceptQueries>();
            var signatures = conceptQueries.GetSignaturesWithDocumentation("SqlObject");
            Assert.AreEqual(1, signatures.Count);
            StringAssert.StartsWith(signatures.Single().Signature, "SqlObject");
        }

        [TestMethod]
        public void ValidForParent()
        {
            var conceptQueries = serviceProvider.GetService<ConceptQueries>();
            var rhetosAppContext = serviceProvider.GetService<RhetosAppContext>();

            var browseInfoType = rhetosAppContext.Keywords["Browse"].First();
            var moduleInfoType = rhetosAppContext.Keywords["Module"].First();
            var entityInfoType = rhetosAppContext.Keywords["Entity"].First();
            var shortStringInfoType = rhetosAppContext.Keywords["ShortString"].First();

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
                CollectionAssert.Contains(validTypes, shortStringInfoType);
            }
        }


        [TestMethod]
        public void RhetosSignatureEntity()
        {
            var conceptQueries = serviceProvider.GetService<ConceptQueries>();
            var signatures = conceptQueries.GetSignaturesWithDocumentation("Entity");
            Console.WriteLine(JsonConvert.SerializeObject(signatures, Formatting.Indented));
            
            Assert.AreEqual(1, signatures.Count);
            var signature = signatures.Single();
            StringAssert.Contains(signature.Documentation, "Defined by EntityInfo");
            Assert.AreEqual("Entity <Module: ModuleInfo>.<Name: String> ", signature.Signature);
            Assert.AreEqual(signature.ConceptInfoType, typeof(EntityInfo));
            Assert.AreEqual("Module: ModuleInfo,Name: String", string.Join(",", signature.Parameters.Select(ConceptInfoType.ConceptMemberDescription)));
        }

        [TestMethod]
        public void RhetosSignatureReference()
        {
            var conceptQueries = serviceProvider.GetService<ConceptQueries>();
            var signatures = conceptQueries.GetSignaturesWithDocumentation("Reference");
            Console.WriteLine(JsonConvert.SerializeObject(signatures, Formatting.Indented));

            Assert.AreEqual(2, signatures.Count);
            var signature = signatures.Single(sig => sig.ConceptInfoType == typeof(ReferencePropertyInfo));
            StringAssert.Contains(signature.Documentation, "Defined by ReferencePropertyInfo");
            Assert.AreEqual("Reference <DataStructure: DataStructureInfo>.<Name: String> <Referenced: DataStructureInfo>", signature.Signature);
            Assert.AreEqual("DataStructure: DataStructureInfo,Name: String,Referenced: DataStructureInfo", string.Join(",", signature.Parameters.Select(ConceptInfoType.ConceptMemberDescription)));
        }

        // TODO: Missing assert
        [TestMethod]
        public void Test()
        {
            var conceptQueries = serviceProvider.GetService<ConceptQueries>();
            var validConcepts = conceptQueries.ValidConceptsForParent(typeof(EntityInfo));

            foreach (var validConcept in validConcepts)
            {
                Console.WriteLine(ConceptInfoHelper.GetKeyword(validConcept));
            }

            /*
            var keys = ConceptInfoType.ConceptInfoKeys(type);
            Console.WriteLine(string.Join(" / ", keys.Select(a => a.Name)));
            */
        }

        // TODO: Missing assert
        [TestMethod]
        public void Test2()
        {
            var type = typeof(SqlDependsOnIDInfo);
            var keys = ConceptInfoType.ConceptInfoKeys(type);
            Console.WriteLine(string.Join(" / ", keys.Select(a => a.Name)));
        }
    }
}
