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
    }
}
