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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Services;

namespace Rhetos.LanguageServices.CodeAnalysis.Test
{
    [TestClass]
    public class ConceptTypeToolsTests
    {
        private readonly IServiceProvider serviceProvider;

        public ConceptTypeToolsTests()
        {
            serviceProvider = TestCommon.CreateTestServiceProvider("./");
        }

        [DataTestMethod]
        [DataRow("EntityInfo", "Entity <Module: ModuleInfo>.<Name: String> ")]
        [DataRow("AllPropertiesLoggingInfo", "AllProperties <EntityLogging: EntityLoggingInfo> ")]
        [DataRow("ShortStringPropertyInfo", "ShortString <DataStructure: DataStructureInfo>.<Name: String> ")]
        [DataRow("ReferencePropertyInfo", "Reference <DataStructure: DataStructureInfo>.<Name: String> <Referenced: DataStructureInfo>")]
        public void SignatureDescriptions(string conceptTypeName, string expectedDescription)
        {
            var rhetosProjectContext = serviceProvider.GetRequiredService<RhetosProjectContext>();
            var conceptType = rhetosProjectContext.DslSyntax.ConceptTypes.Single(a => a.TypeName == conceptTypeName);
            var description = ConceptTypeTools.SignatureDescription(conceptType);
            Assert.AreEqual(expectedDescription, description);
        }
    }
}
