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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.Dsl.DefaultConcepts;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class ConceptInfoTypeTests
    {
        [DataTestMethod]
        [DataRow(typeof(EntityInfo), "Entity <Module: ModuleInfo>.<Name: String> ")]
        [DataRow(typeof(AllPropertiesLoggingInfo), "AllProperties <EntityLogging: EntityLoggingInfo> ")]
        [DataRow(typeof(ShortStringPropertyInfo), "ShortString <DataStructure: DataStructureInfo>.<Name: String> ")]
        [DataRow(typeof(ReferencePropertyInfo), "Reference <DataStructure: DataStructureInfo>.<Name: String> <Referenced: DataStructureInfo>")]
        public void SignatureDescriptions(Type conceptInfoType, string expectedDescription)
        {
            var description = ConceptInfoType.SignatureDescription(conceptInfoType);
            Assert.AreEqual(expectedDescription, description);
        }
    }
}
