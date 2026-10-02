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
using Microsoft.Extensions.Logging;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.CodeAnalysis.Tools;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.CommonTestTools
{
    public static class TestCommon
    {
        /// <summary>
        /// A file document key in a folder that does not exist, in the canonical form that the language server creates from an LSP document URI.
        /// </summary>
        public static DocumentKey DocumentKeyMock => DocumentKey.ForFile("file:///c:/stub/stub.rhe", @"c:\stub\stub.rhe");

        /// <summary>
        /// Creates a file document key from an absolute file path. The drive letter case of the path is kept.
        /// </summary>
        public static DocumentKey DocumentKeyFromPath(string localPath) => DocumentKey.ForFile(new Uri(localPath).AbsoluteUri, localPath);

        public static IServiceProvider CreateTestServiceProvider(string initializeContextFromFolder = null)
        {
            var services = new ServiceCollection()
                .AddSingleton<RhetosWorkspace>()
                .AddTransient<RhetosDocumentFactory>()
                .AddSingleton<RhetosProjectContext>()
                .AddSingleton<IRhetosProjectRootPathResolver, RhetosProjectRootPathResolver>()
                .AddSingleton<ILogProvider, RhetosNetCoreLogProvider>()
                .AddSingleton<ConceptQueries>()
                .AddLogging(cfg => cfg.AddConsole());

            if (initializeContextFromFolder != null)
                services.AddSingleton<IRhetosProjectRootPathResolver, RootPathResolverMock>();

            var serviceProvider = services.BuildServiceProvider();

            if (initializeContextFromFolder != null)
            {
                var rhetosProjectContext = serviceProvider.GetRequiredService<RhetosProjectContext>();
                var logProvider = serviceProvider.GetRequiredService<ILogProvider>();
                rhetosProjectContext.Initialize(new DslSyntaxProviderMock(initializeContextFromFolder, logProvider));
            }

            return serviceProvider;
        }

        public static RhetosDocument CreateWithTestUri(this RhetosDocumentFactory rhetosDocumentFactory, string text = null, LineChr? showPosition = null)
        {
            var name = Guid.NewGuid();
            var rhetosDocument = rhetosDocumentFactory.CreateNew(DocumentKey.ForFile($"file:///c:/stub/{name}.rhe", $@"c:\stub\{name}.rhe"));
            if (text != null)
            {
                rhetosDocument.UpdateText(text);
                Console.WriteLine($"Initialized document: {rhetosDocument.DocumentKey} with text:\n{text}<< END DOCUMENT >>\n\n");
            }

            if (showPosition != null)
            {
                var positionText = rhetosDocument.TextDocument.ShowPosition(showPosition.Value);
                Console.WriteLine($"\n{positionText}\n");
            }

            return rhetosDocument;
        }

        public static string ToEndings(this string text, EndingsStyle endingsStyle)
        {
            switch (endingsStyle)
            {
                case EndingsStyle.Linux:
                    return text.ToLinuxEndings();
                case EndingsStyle.Windows:
                    return text.ToWindowsEndings();
                default:
                    throw new NotImplementedException();
            }
        }

        public static string ToLinuxEndings(this string text)
        {
            return text.Replace("\r\n", "\n");
        }

        public static string ToWindowsEndings(this string text)
        {
            return text.ToLinuxEndings().Replace("\n", "\r\n");
        }
    }
}
