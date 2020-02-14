using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
