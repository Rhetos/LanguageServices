using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.Dsl;
using Rhetos.Dsl.DefaultConcepts;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;
using PropertyInfo = Rhetos.Dsl.DefaultConcepts.PropertyInfo;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class ConceptQueriesTests
    {
        private readonly RhetosAppContext rhetosAppContext;

        public ConceptQueriesTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            var logFactory = LoggerFactory.Create(b => b.AddConsole());
            rhetosAppContext = new RhetosAppContext(logFactory);
            rhetosAppContext.InitializeFromCurrentDomain();
        }

        [TestMethod]
        public void Test()
        {
            var conceptQueries = new ConceptQueries(rhetosAppContext);
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

        [TestMethod]
        public void Test2()
        {
            var type = typeof(SqlDependsOnIDInfo);
            var keys = ConceptInfoType.ConceptInfoKeys(type);
            Console.WriteLine(string.Join(" / ", keys.Select(a => a.Name)));
        }
    }
}
