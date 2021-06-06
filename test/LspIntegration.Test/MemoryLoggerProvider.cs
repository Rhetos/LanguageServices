using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Rhetos.LanguageServices.LspIntegration.Test
{
    public class MemoryLoggerProvider : ILoggerProvider
    {
        private class Logger : ILogger
        {
            private readonly string category;
            private readonly MemoryLoggerProvider provider;

            public Logger(string category, MemoryLoggerProvider provider)
            {
                this.category = category;
                this.provider = provider;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                provider.Logs.Add($"[{logLevel}] | {category} | {formatter(state, exception)}");
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state)
            {
                return Disposable.Empty;
            }
        }

        public List<string> Logs { get; } = new();
        public MemoryLoggerProvider()
        {
        }

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new Logger(categoryName, this);
        }
    }
}
