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

            Assert.AreEqual((0, 0), doc.GetLineChr(0));
            Assert.AreEqual((0, 3), doc.GetLineChr(3));
            Assert.AreEqual((0, 4), doc.GetLineChr(4));
            Assert.AreEqual((2, 0), doc.GetLineChr(6));
            Assert.AreEqual((2, 1), doc.GetLineChr(7));
            Assert.AreEqual((3, 0), doc.GetLineChr(text.Length - 2));
            Assert.AreEqual((4, 0), doc.GetLineChr(text.Length));
            Assert.AreEqual((4, 0), doc.GetLineChr(text.Length + 1));
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

            Assert.AreEqual((0, 0), doc.GetLineChr(0));
            Assert.AreEqual((0, 3), doc.GetLineChr(3));
            Assert.AreEqual((0, 4), doc.GetLineChr(4));
            Assert.AreEqual((2, 0), doc.GetLineChr(8));
            Assert.AreEqual((2, 1), doc.GetLineChr(9));
            Assert.AreEqual((3, 0), doc.GetLineChr(text.Length - 3));
            Assert.AreEqual((4, 0), doc.GetLineChr(text.Length));
            Assert.AreEqual((4, 0), doc.GetLineChr(text.Length + 1));
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
            Assert.AreEqual((1, 0), doc.GetLineChr(text.Length - 1));
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

        [TestMethod]
        public void ShowPosition()
        {
            var line = "Line test ble\n";
            Func<int, string> leadSpaces = spaces => "^".PadLeft(spaces + 1);
            Assert.AreEqual(line + leadSpaces(0), TextDocument.ShowPositionOnLine(line, 0));
            Assert.AreEqual(line + leadSpaces(4), TextDocument.ShowPositionOnLine(line, 4));
            Assert.AreEqual(line + leadSpaces(13), TextDocument.ShowPositionOnLine(line, 13));
            Assert.AreEqual(line + leadSpaces(13), TextDocument.ShowPositionOnLine(line, 14));
        }

        /*
        [TestMethod]
        public void LastPosition()
        {
            var text = "0123\r\n\r\n";
            var doc = new TextDocument(text);
            var pos = ;

            Console.WriteLine($"Char at pos {pos} is {(int)doc.Text[pos]}.");
            var lineChr = doc.GetLineChr(pos);
            Console.WriteLine(lineChr);
            Console.WriteLine(doc.ShowPosition(lineChr.line, lineChr.chr));
        }*/
    }
}
