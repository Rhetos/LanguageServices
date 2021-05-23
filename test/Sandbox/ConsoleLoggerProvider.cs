using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Logging;

namespace Sandbox
{
    public class ConsoleLoggerProvider : ILogProvider
    {
        public ILogger GetLogger(string eventName)
        {
            return new ConsoleLogger(eventName);
        }
    }
}
