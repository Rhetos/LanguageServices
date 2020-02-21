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
        public string Context { get; }

        public RootPathConfiguration(string rootPath, RootPathConfigurationType configurationType, string context)
        {
            this.RootPath = rootPath;
            this.ConfigurationType = configurationType;
            this.Context = context;
        }
    }
}
