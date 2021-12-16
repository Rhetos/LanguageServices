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
        public static Uri UriMock => new Uri("file://\\stub.txt");

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
            var rhetosDocument = rhetosDocumentFactory.CreateNew(new Uri($"file://\\{Guid.NewGuid()}"));
            if (text != null)
            {
                rhetosDocument.UpdateText(text);
                Console.WriteLine($"Initialized document: {rhetosDocument.DocumentUri} with text:\n{text}<< END DOCUMENT >>\n\n");
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
