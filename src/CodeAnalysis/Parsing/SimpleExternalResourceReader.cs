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

using Rhetos.Dsl;
using Rhetos.Utilities;
using System;
using System.Collections.Generic;

namespace Rhetos.LanguageServices.CodeAnalysis.Parsing
{
    /// <summary>
    /// This class is just a placeholder for reading external text files, referenced from DSL scripts.
    /// For DSL syntax check and IntelliSense, it is safer to skip loading external files and resources.
    /// </summary>
    internal class SimpleExternalResourceReader : IExternalTextReader
    {
        public ValueOrError<string> Read(DslScript dslScript, string relativePathOrResourceName)
        {
            return $"<{relativePathOrResourceName}>";
        }

        public IReadOnlyCollection<string> ExternalFiles => Array.Empty<string>();
    }
}