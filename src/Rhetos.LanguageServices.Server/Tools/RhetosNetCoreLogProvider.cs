using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rhetos.LanguageServices.Server.Tools
{
    public class RhetosNetCoreLogProvider : Rhetos.Logging.ILogProvider
    {
        private readonly Microsoft.Extensions.Logging.ILoggerFactory logFactory;

        public RhetosNetCoreLogProvider(Microsoft.Extensions.Logging.ILoggerFactory logFactory)
        {
            this.logFactory = logFactory;
        }

        public Rhetos.Logging.ILogger GetLogger(string loggerName)
        {
            var netCoreLogger = logFactory.CreateLogger(loggerName);
            return new RhetosNetCoreLogger(netCoreLogger, loggerName);
        }
    }
}
