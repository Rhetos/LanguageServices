using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rhetos.LanguageServices.Server.Services;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Test
{
    public static class TestCommon
    {
        public static IServiceProvider CreateTestServiceProvider()
        {
            var services = new ServiceCollection()
                .AddSingleton<RhetosWorkspace>()
                .AddTransient<RhetosDocumentFactory>()
                .AddSingleton<RhetosAppContext>()
                .AddSingleton<XmlDocumentationProvider>()
                .AddSingleton<ILogProvider, RhetosNetCoreLogProvider>()
                .AddSingleton<ConceptQueries>()
                .AddLogging(cfg => cfg.AddConsole());

            return services.BuildServiceProvider();
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
