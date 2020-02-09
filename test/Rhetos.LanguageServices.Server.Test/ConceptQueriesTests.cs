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
