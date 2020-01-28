using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog.Targets;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.Logging;
using Rhetos.Utilities;
using Rhetos.Utilities.ApplicationConfiguration;
using DslParser = Rhetos.LanguageServices.Server.RhetosTmp.DslParser;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class DslParserTests
    {
        private readonly RhetosAppContext rhetosAppContext;
        public DslParserTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            rhetosAppContext = new RhetosAppContext(loggerFactory.CreateLogger<RhetosAppContext>());
            rhetosAppContext.InitializeFromCurrentDomain();
        }

        [TestMethod]
        public void InitializeFromCurrentDomain()
        {
            Console.WriteLine($"Keywords: {rhetosAppContext.Keywords.Count}, ConceptTypes: {rhetosAppContext.ConceptInfoTypes.Length}.");
        }


        [TestMethod]
        public void Test1()
        {
            var script = @"
Module TestModule
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
            var logProvider = new NLogProvider();
            var rheDocument = new RheDocument(script, rhetosAppContext, logProvider);

            var dslParser = new DslParser(rheDocument.Tokenizer, rhetosAppContext.ConceptInfoInstances, logProvider);
            var parsedConcepts = dslParser.ParseConceptsWithCallbacks(
                (tokenReader, keyword) =>
                {
                    var reader = tokenReader as TokenReader;
                    var token = rheDocument.Tokenizer.GetTokens()[reader.PositionInTokenList];
                    Console.WriteLine($"OnKeyword, token={token.Value}, keyword={keyword}.");
                },
                OnMemberRead,
                null);

            Console.WriteLine(parsedConcepts.Count());
            foreach (var parsedConcept in parsedConcepts)
            {
                Console.WriteLine($"{parsedConcept.GetType().Name}: {parsedConcept.GetKey()}");
            }
        }

        private void OnMemberRead(ITokenReader tokenReader, Type conceptInfoType, ConceptMember member, ValueOrError<object> value)
        {
            Console.WriteLine($"OnMemberRead: conceptInfo={conceptInfoType.Name}, member={member.Name}, value={value.ToString()}.");
        }

        [TestMethod]
        public void DslModel()
        {
            var rhetosAppEnvironment = RhetosAppEnvironmentProvider.Load(@"C:\Projects\RhetosSasa\Source\Rhetos");
            var configuration = new ConfigurationBuilder()
                .AddRhetosAppEnvironment(rhetosAppEnvironment)
                .AddKeyValue("ConnectionStrings:ServerConnectionString:ConnectionString", "stub")
                .Build();

            var containerBuilder = new RhetosContainerBuilder(configuration, new NLogProvider(), LegacyUtilities.GetListAssembliesDelegate(configuration));
            containerBuilder.AddRhetosRuntime();
            var container = containerBuilder.Build();

            var dslModel = container.Resolve<IDslModel>();
            Console.WriteLine(dslModel.Concepts.Count());
            foreach (var conceptInfo in dslModel.Concepts.Where(a => a.GetKey().Contains("GlavniModul")))
            {
                Console.WriteLine(conceptInfo.GetKey());
            }
        }
    }
}
