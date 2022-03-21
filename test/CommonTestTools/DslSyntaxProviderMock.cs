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
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;
using Rhetos.LanguageServices.CodeAnalysis.Tools;
using Rhetos.Logging;
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.CommonTestTools
{
    public class DslSyntaxProviderMock : IDslSyntaxProvider
    {
        private readonly ILogProvider logProvider;

        public string ProjectRootPath { get; }
        public DateTime LastModifiedTime { get; init; } = DateTime.Now;
        public DslDocumentation DslDocumentation { get; init; }

        public DslSyntaxProviderMock(string explicitFolderWithDslSyntax, ILogProvider logProvider)
        {
            ProjectRootPath = explicitFolderWithDslSyntax;
            this.logProvider = logProvider;
        }

        public DslSyntax Load()
        {
            return new DslSyntaxFile(new RhetosBuildEnvironment() {CacheFolder = ProjectRootPath}, logProvider).Load();
        }

        public DslDocumentation LoadDocumentation()
        {
            if (DslDocumentation != null)
                return DslDocumentation;

            return new DslDocumentationFile(new RhetosBuildEnvironment() { CacheFolder = ProjectRootPath }).Load();
        }

        public DateTime GetLastModifiedTime()
        {
            return LastModifiedTime;
        }
    }
}
