using System;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.LanguageServices.Server.Services;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Rhetos.LanguageServices.Server.Test
{
    /// <summary>
    /// Test class
    /// Second line documentation
    /// </summary>
    [TestClass]
    public class XmlDocumentationProviderTests
    {
        private readonly ILoggerFactory loggerFactory;

        public XmlDocumentationProviderTests()
        {
            loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));
        }

        [TestMethod]
        public void XmlDocForType()
        {
            var provider = new XmlDocumentationProvider(loggerFactory.CreateLogger<XmlDocumentationProvider>());
            var xmlDoc = provider.GetDocumentation(typeof(XmlDocumentationProviderTests));

            Console.WriteLine(xmlDoc);
            Assert.AreEqual("Test class\nSecond line documentation", xmlDoc);
        }
    }
}
