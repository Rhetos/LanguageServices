using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rhetos.LanguageServices.CodeAnalysis.Tools
{
    public class AppLanguageServicesOptions
    {
        public static readonly string ConfigurationFilename = "rhetos-language-services.settings.json";

        public string RhetosProjectRootPath { get; set; }
    }
}
