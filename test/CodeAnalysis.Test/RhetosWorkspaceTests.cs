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
using Rhetos.LanguageServices.CommonTestTools;

namespace Rhetos.LanguageServices.CodeAnalysis.Test
{
    [TestClass]
    [DeploymentItem("DslSyntax.json")]
    public class RhetosWorkspaceTests
    {
        private static readonly DocumentKey openedKey = DocumentKey.ForFile("file:///c:/stub/Folder/Doc.rhe", @"c:\stub\Folder\Doc.rhe");
        private static readonly DocumentKey differentCaseKey = DocumentKey.ForFile("file:///c:/STUB/folder/doc.RHE", @"c:\STUB\folder\doc.RHE");

        private static RhetosWorkspace CreateWorkspace() => TestCommon.CreateTestServiceProvider().GetRequiredService<RhetosWorkspace>();

        [TestMethod]
        public void DocumentUriComparerMatchesPlatform()
        {
            var expected = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            Assert.AreSame(expected, RhetosWorkspace.DocumentUriComparer);
        }

        [TestMethod]
        public void UpdateWithDifferentCaseKeepsOpenedKey()
        {
            if (OperatingSystem.IsLinux())
                Assert.Inconclusive("Document URIs are case-sensitive on Linux.");

            var workspace = CreateWorkspace();
            workspace.UpdateDocumentText(openedKey, "Module A;");
            var openedDocument = workspace.GetRhetosDocument(openedKey.Uri);

            workspace.UpdateDocumentText(differentCaseKey, "Module B;");

            var document = workspace.GetRhetosDocument(differentCaseKey.Uri);
            Assert.AreSame(openedDocument, document);
            Assert.AreEqual("Module B;", document.TextDocument.Text);
            Assert.AreSame(openedKey, document.DocumentKey);
            Assert.AreSame(openedKey, document.TextDocument.DocumentKey);
            CollectionAssert.AreEqual(new[] { openedKey.Uri }, workspace.GetUpdatedDocuments(DateTime.MinValue));
            Assert.AreEqual(0, workspace.GetClosedDocuments(DateTime.MinValue).Count);
        }

        [TestMethod]
        public void CloseWithDifferentCaseReportsOpenedUri()
        {
            if (OperatingSystem.IsLinux())
                Assert.Inconclusive("Document URIs are case-sensitive on Linux.");

            var workspace = CreateWorkspace();
            workspace.UpdateDocumentText(openedKey, "Module A;");
            workspace.CloseDocument(differentCaseKey.Uri);

            Assert.AreEqual(0, workspace.GetUpdatedDocuments(DateTime.MinValue).Count);
            CollectionAssert.AreEqual(new[] { openedKey.Uri }, workspace.GetClosedDocuments(DateTime.MinValue));
            Assert.ThrowsExactly<InvalidOperationException>(() => workspace.GetRhetosDocument(openedKey.Uri));
        }

        [TestMethod]
        public void ReopenedDocumentIsNotReportedAsClosed()
        {
            var workspace = CreateWorkspace();
            workspace.UpdateDocumentText(openedKey, "Module A;");
            workspace.CloseDocument(openedKey.Uri);
            var reopenedKey = DocumentKey.ForFile(openedKey.Uri, openedKey.LocalPath);
            workspace.UpdateDocumentText(reopenedKey, "Module A;");

            CollectionAssert.AreEqual(new[] { openedKey.Uri }, workspace.GetUpdatedDocuments(DateTime.MinValue));
            Assert.AreEqual(0, workspace.GetClosedDocuments(DateTime.MinValue).Count);
            Assert.AreSame(reopenedKey, workspace.GetRhetosDocument(openedKey.Uri).DocumentKey);
        }

        [TestMethod]
        public void ChangesAfterSinceTime()
        {
            var workspace = CreateWorkspace();
            workspace.UpdateDocumentText(openedKey, "Module A;");
            var otherKey = DocumentKey.ForNonFile("untitled:Untitled-1");
            workspace.UpdateDocumentText(otherKey, "Module B;");

            CollectionAssert.AreEquivalent(new[] { openedKey.Uri, otherKey.Uri }, workspace.GetUpdatedDocuments(DateTime.MinValue));
            Assert.AreEqual(0, workspace.GetUpdatedDocuments(DateTime.Now.AddMinutes(1)).Count);

            workspace.CloseDocument(otherKey.Uri);
            CollectionAssert.AreEqual(new[] { otherKey.Uri }, workspace.GetClosedDocuments(DateTime.MinValue));
            Assert.AreEqual(0, workspace.GetClosedDocuments(DateTime.Now.AddMinutes(1)).Count);
        }

        [TestMethod]
        public void UnknownDocumentErrorContainsUri()
        {
            var workspace = CreateWorkspace();
            workspace.UpdateDocumentText(openedKey, "Module A;");

            var unknownUri = "file:///c:/stub/Other%20Folder/%C4%8D.rhe";
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => workspace.GetRhetosDocument(unknownUri));
            StringAssert.Contains(exception.Message, $"'{unknownUri}'", StringComparison.Ordinal);
        }

        [TestMethod]
        public void NonFileDocumentErrorsHavePositions()
        {
            // The analysis must not fail on a non-file document: Rhetos reports error positions only for a non-empty DslScript.Path.
            var documentFactory = TestCommon.CreateTestServiceProvider("./").GetRequiredService<RhetosDocumentFactory>();
            var rhetosDocument = documentFactory.CreateNew(DocumentKey.ForNonFile("untitled:Untitled-1"));
            rhetosDocument.UpdateText("Module M\n{\n    Entity E\n    {\n    }\n    error\n}\n");
            var tokenizerErrorDocument = documentFactory.CreateNew(DocumentKey.ForNonFile("untitled:Untitled-2"));
            tokenizerErrorDocument.UpdateText("Module 'M\n{\n}\n");

            var parserErrors = rhetosDocument.GetAnalysis().AllErrors.ToList();
            var tokenizerErrors = tokenizerErrorDocument.GetAnalysis().TokenizerErrors;
            Console.WriteLine(string.Join(Environment.NewLine, parserErrors.Concat(tokenizerErrors)));

            Assert.AreNotEqual(0, parserErrors.Count);
            Assert.IsTrue(parserErrors.All(error => error.BeginLineChr.Line > 0), "Parser errors should have positions after the first line.");
            Assert.AreEqual(1, tokenizerErrors.Count);
            Assert.AreEqual("RH0008", tokenizerErrors[0].Code);
        }
    }
}
