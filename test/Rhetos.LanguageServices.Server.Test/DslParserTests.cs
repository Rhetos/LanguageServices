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
using System.Linq;
using System.Reflection;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.Logging;
using Rhetos.Utilities.ApplicationConfiguration;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class DslParserTests
    {
        private readonly IServiceProvider serviceProvider;
        private readonly RhetosAppContext rhetosAppContext;

        public DslParserTests()
        {
            Assembly.Load("Rhetos.Dsl.DefaultConcepts");
            serviceProvider = TestCommon.CreateTestServiceProvider();

            rhetosAppContext =  serviceProvider.GetService<RhetosAppContext>();
            rhetosAppContext.InitializeFromCurrentDomain();
        }

        [TestMethod]
        public void InitializeFromCurrentDomain()
        {
            Console.WriteLine($"Keywords: {rhetosAppContext.Keywords.Count}, ConceptTypes: {rhetosAppContext.ConceptInfoTypes.Length}.");
        }

        [TestMethod]
        public void DslModel()
        {
            var rhetosAppEnvironment = RhetosAppEnvironmentProvider.Load(@"C:\Projects\RhetosSasa\Source\Rhetos");
            var configuration = new ConfigurationBuilder()
                .AddRhetosAppEnvironment(rhetosAppEnvironment)
                .AddKeyValue("ConnectionStrings:ServerConnectionString:ConnectionString", "stub")
                .Build();

            var containerBuilder = new RhetosContainerBuilder(configuration, serviceProvider.GetService<ILogProvider>(), LegacyUtilities.GetListAssembliesDelegate(configuration));
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
