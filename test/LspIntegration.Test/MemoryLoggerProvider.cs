/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

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
