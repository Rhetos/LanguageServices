using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Logging;

namespace Sandbox
{
    public class ConsoleLogger : ILogger
    {
        public string Name { get; }

        public ConsoleLogger(string name)
        {
            Name = name;
        }

        public void Write(EventType eventType, Func<string> logMessage)
        {
            Console.WriteLine($"[{eventType}] {logMessage()}");
        }
    }
}
