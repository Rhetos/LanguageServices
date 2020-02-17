using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Test
{
    [TestClass]
    public class TextDocumentTests
    {
        [TestMethod]
        public void PositionConversionsLinuxStyle()
        {
            var text = "0123\n" +
                       "\n" +
                       "0\n" +
                       "\n" +
                       "\n";

            var doc = new TextDocument(text);

            Assert.AreEqual(0, doc.GetPosition(0, 0));
            Assert.AreEqual(4, doc.GetPosition(0, 4));
            Assert.AreEqual(4, doc.GetPosition(0, 5));
            Assert.AreEqual(4, doc.GetPosition(0, 6));
            Assert.AreEqual(6, doc.GetPosition(2, 0));
            Assert.AreEqual(7, doc.GetPosition(2, 1));
            Assert.AreEqual(8, doc.GetPosition(3, 0));
            Assert.AreEqual(8, doc.GetPosition(3, 1));
            Assert.AreEqual(8, doc.GetPosition(3, 2));
            Assert.AreEqual(text.Length - 1, doc.GetPosition(6, 3));

            Assert.AreEqual(new LineChr(0, 0), doc.GetLineChr(0));
            Assert.AreEqual(new LineChr(0, 3), doc.GetLineChr(3));
            Assert.AreEqual(new LineChr(0, 4), doc.GetLineChr(4));
            Assert.AreEqual(new LineChr(2, 0), doc.GetLineChr(6));
            Assert.AreEqual(new LineChr(2, 1), doc.GetLineChr(7));
            Assert.AreEqual(new LineChr(3, 0), doc.GetLineChr(text.Length - 2));
            Assert.AreEqual(new LineChr(4, 0), doc.GetLineChr(text.Length));
            Assert.AreEqual(new LineChr(4, 0), doc.GetLineChr(text.Length + 1));
        }

        [TestMethod]
        public void PositionConversionsWindowsStyle()
        {
            var text = "0123\r\n" +
                       "\r\n" +
                       "0\r\n" +
                       "\r\n" +
                       "\r\n";
            var doc = new TextDocument(text);

            Assert.AreEqual(0, doc.GetPosition(0, 0));
            Assert.AreEqual(4, doc.GetPosition(0, 4));
            Assert.AreEqual(4, doc.GetPosition(0, 5));
            Assert.AreEqual(4, doc.GetPosition(0, 6));
            Assert.AreEqual(8, doc.GetPosition(2, 0));
            Assert.AreEqual(9, doc.GetPosition(2, 1));
            Assert.AreEqual(11, doc.GetPosition(3, 0));
            Assert.AreEqual(11, doc.GetPosition(3, 1));
            Assert.AreEqual(11, doc.GetPosition(3, 2));
            Assert.AreEqual(text.Length - 2, doc.GetPosition(6, 3));

            Assert.AreEqual(new LineChr(0, 0), doc.GetLineChr(0));
            Assert.AreEqual(new LineChr(0, 3), doc.GetLineChr(3));
            Assert.AreEqual(new LineChr(0, 4), doc.GetLineChr(4));
            Assert.AreEqual(new LineChr(2, 0), doc.GetLineChr(8));
            Assert.AreEqual(new LineChr(2, 1), doc.GetLineChr(9));
            Assert.AreEqual(new LineChr(3, 0), doc.GetLineChr(text.Length - 3));
            Assert.AreEqual(new LineChr(4, 0), doc.GetLineChr(text.Length));
            Assert.AreEqual(new LineChr(4, 0), doc.GetLineChr(text.Length + 1));
        }

        [TestMethod]
        public void PositionConversionLastLine()
        {
            var text =
                "0123\n" +
                "0";

            var doc = new TextDocument(text);

            Assert.AreEqual(5, doc.GetPosition(1, 0));
            Assert.AreEqual(5, doc.GetPosition(2, 0));
            Assert.AreEqual(5, doc.GetPosition(1, 1));
            Assert.AreEqual(new LineChr(1, 0), doc.GetLineChr(text.Length - 1));
        }

        [TestMethod]
        public void ExtractLine()
        {
            var text =
                "0123\n" +
                "\n" +
                "0";

            var doc = new TextDocument(text);
            Assert.AreEqual("0123\n", doc.ExtractLine(0));
            Assert.AreEqual("0123\n", doc.ExtractLine(1));
            Assert.AreEqual("0123\n", doc.ExtractLine(4));
            Assert.AreEqual("\n", doc.ExtractLine(5));
            Assert.AreEqual("0", doc.ExtractLine(6));
        }

        [DataTestMethod]
        [DataRow(EndingsStyle.Linux, DisplayName = "Linux")]
        [DataRow(EndingsStyle.Windows, DisplayName = "Windows")]
        public void ShowPosition(EndingsStyle endingsStyle)
        {
            var line = "Line test ble\n";
            line = line.ToEndings(endingsStyle);
            Func<int, string> leadSpaces = spaces => "^".PadLeft(spaces + 1);
            Assert.AreEqual(line + leadSpaces(0), TextDocument.ShowPositionOnLine(line, 0));
            Assert.AreEqual(line + leadSpaces(4), TextDocument.ShowPositionOnLine(line, 4));
            Assert.AreEqual(line + leadSpaces(13), TextDocument.ShowPositionOnLine(line, 13));
            Assert.AreEqual(line + leadSpaces(13), TextDocument.ShowPositionOnLine(line, 14));
        }

        [TestMethod]
        public void EmptyDocument()
        {
            var doc = new TextDocument("");

            Assert.AreEqual(0, doc.GetPosition(0, 0));
            Assert.AreEqual(0, doc.GetPosition(0, 10));
            Assert.AreEqual(0, doc.GetPosition(10, 0));
            Assert.AreEqual(0, doc.GetPosition(10, 10));

            Assert.AreEqual(LineChr.Zero, doc.GetLineChr(0));
            Assert.AreEqual(LineChr.Zero, doc.GetLineChr(1));
        }

        [DataTestMethod]
        [DataRow(EndingsStyle.Linux, DisplayName = "Linux")]
        [DataRow(EndingsStyle.Windows, DisplayName = "Windows")]
        public void SingleEmptyLine(EndingsStyle endingsStyle)
        {
            var text = "\n";
            text = text.ToEndings(endingsStyle);
            var doc = new TextDocument(text);

            Assert.AreEqual(0, doc.GetPosition(0, 1));
            Assert.AreEqual(0, doc.GetPosition(0, 5));
            Assert.AreEqual(0, doc.GetPosition(1, 1));
            Assert.AreEqual(0, doc.GetPosition(1, 5));
            Assert.AreEqual(0, doc.GetPosition(2, 5));

            Assert.AreEqual(LineChr.Zero, doc.GetLineChr(0));
            Assert.AreEqual(LineChr.Zero, doc.GetLineChr(1));
            Assert.AreEqual(LineChr.Zero, doc.GetLineChr(2));
            Assert.AreEqual(LineChr.Zero, doc.GetLineChr(3));
        }

        [DataTestMethod]
        [DataRow(0, 0, "\n")]
        [DataRow(1, 0, "// comment\n")]
        [DataRow(4, 0, "\n\n")]
        [DataRow(4, 1, "\n\n")]
        [DataRow(4, 2, "\n\n")]
        [DataRow(6, 0, "{\n")]
        [DataRow(6, 10, "{\n")]
        [DataRow(9, 0, "}\n")]
        [DataRow(10, 0, "\n\n")]
        [DataRow(11, 0, "\n\n")]
        public void CorrectlyTruncates(int line, int chr, string expectedEndsWith)
        {
            var testText =
@"
// comment
Module
{

    Entity
    {
        ShortString;
    }
}

";
            var variants = new[]
            {
                (endingsStyle: EndingsStyle.Linux, name: "Linux"),
                (endingsStyle: EndingsStyle.Windows, name: "Windows")
            };

            foreach (var variant in variants)
            {
                var text = testText.ToEndings(variant.endingsStyle);
                var textDocument = new TextDocument(text);
                var truncated = textDocument.GetTruncatedAtNextEndOfLine(new LineChr(line, chr));
                StringAssert.EndsWith(truncated, expectedEndsWith.ToEndings(variant.endingsStyle), variant.name);
            }
        }

        [TestMethod]
        public void CorrectlyTruncatesEmptyString()
        {
            var textDocument = new TextDocument("");
            Assert.AreEqual("", textDocument.GetTruncatedAtNextEndOfLine(new LineChr(0, 0)));
            Assert.AreEqual("", textDocument.GetTruncatedAtNextEndOfLine(new LineChr(0, 1)));
            Assert.AreEqual("", textDocument.GetTruncatedAtNextEndOfLine(new LineChr(1, 0)));
        }
    }
}
