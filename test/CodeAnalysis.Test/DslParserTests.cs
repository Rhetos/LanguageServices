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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;

namespace Rhetos.LanguageServices.CodeAnalysis.Test
{
    [TestClass]
    public class DslParserTests
    {
        private readonly IServiceProvider serviceProvider;
        private readonly RhetosProjectContext rhetosProjectContext;

        public DslParserTests()
        {
            serviceProvider = TestCommon.CreateTestServiceProvider();

            rhetosProjectContext =  serviceProvider.GetService<RhetosProjectContext>();
            rhetosProjectContext.InitializeFromPath("");
        }

        [TestMethod]
        public void InitializeFromCurrentDomain()
        {
            Console.WriteLine($"Keywords: {rhetosProjectContext.Keywords.Count}, ConceptTypes: {rhetosProjectContext.DslSyntax.ConceptTypes.Count}.");
        }
    }
}
