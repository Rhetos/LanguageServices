﻿/*
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
