using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rhetos.Logging;

namespace Rhetos.LanguageServices.Server.Tools
{
    public class RhetosNetCoreLogger : Rhetos.Logging.ILogger
    {
        private readonly Microsoft.Extensions.Logging.ILogger netCoreLogger;
        public string Name { get; }

        public RhetosNetCoreLogger(Microsoft.Extensions.Logging.ILogger netCoreLogger, string name)
        {
            this.netCoreLogger = netCoreLogger;
            this.Name = name;
        }


        public void Write(EventType eventType, Func<string> logMessage)
        {
            switch (eventType)
            {
                case EventType.Trace:
                    netCoreLogger.LogTrace(logMessage());
                    break;
                case EventType.Info:
                    netCoreLogger.LogInformation(logMessage());
                    break;
                case EventType.Error:
                    netCoreLogger.LogError(logMessage());
                    break;
            }
        }
    }
}
