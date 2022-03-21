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
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Services;
using Rhetos.LanguageServices.CodeAnalysis.Tools;

namespace Rhetos.LanguageServices.CommonTestTools
{
    // Mock class which will always resolve root path to the one RhetosProjectContext is initialized with
    public class RootPathResolverMock : IRhetosProjectRootPathResolver
    {
        private readonly RhetosProjectContext rhetosProjectContext;

        public RootPathResolverMock(RhetosProjectContext rhetosProjectContext)
        {
            this.rhetosProjectContext = rhetosProjectContext;
        }
        public RootPathConfiguration ResolveRootPathFromDocumentDirective(RhetosDocument rhetosDocument)
        {
            return new RootPathConfiguration(rhetosProjectContext.ProjectRootPath, RootPathConfigurationType.DetectedRhetosApp, "");
        }

        public RootPathConfiguration ResolveRootPathForDocument(RhetosDocument rhetosDocument)
        {
            return new RootPathConfiguration(rhetosProjectContext.ProjectRootPath, RootPathConfigurationType.DetectedRhetosApp, "");
        }
    }
}
