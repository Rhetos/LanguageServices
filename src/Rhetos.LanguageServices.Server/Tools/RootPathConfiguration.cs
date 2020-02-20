using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rhetos.LanguageServices.Server.Tools
{
    public class RootPathConfiguration
    {
        public string RootPath { get; }
        public RootPathConfigurationType ConfigurationType { get; }
        public string Source { get; }

        public RootPathConfiguration(string rootPath, RootPathConfigurationType configurationType, string source)
        {
            this.RootPath = rootPath;
            this.ConfigurationType = configurationType;
            this.Source = source;
        }
    }
}
