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
using System.IO;
using Microsoft.Extensions.Logging;
using Rhetos.Dsl;
using Rhetos.Logging;
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.CodeAnalysis.Tools
{
    public class DslSyntaxProvider : IDslSyntaxProvider
    {
        public string ProjectRootPath { get; }
        private readonly ILogProvider logProvider;

        public DslSyntaxProvider(string projectRootPath, ILogProvider logProvider)
        {
            ProjectRootPath = projectRootPath;
            this.logProvider = logProvider;
        }

        public DslSyntax Load()
        {
            if (!IsValidProjectRootPath(ProjectRootPath))
                throw new InvalidOperationException($"Can't load {nameof(DslSyntax)} from '{ProjectRootPath}', because it is not a valid project root path.");

            var rhetosBuildEnvironment = new RhetosBuildEnvironment() { CacheFolder = BuildCacheFolder(ProjectRootPath) };
            return new DslSyntaxFile(rhetosBuildEnvironment, logProvider).Load();
        }

        public DslDocumentation LoadDocumentation()
        {
            if (!IsValidProjectRootPath(ProjectRootPath))
                throw new InvalidOperationException($"Can't load {nameof(DslDocumentation)} from '{ProjectRootPath}', because it is not a valid project root path.");

            // for now we are not sure if documentation file is a mandatory output file for Rhetos build process
            // we assume it is not and in case of failure we silently return null
            try
            {
                var rhetosBuildEnvironment = new RhetosBuildEnvironment() {CacheFolder = BuildCacheFolder(ProjectRootPath)};
                return new DslDocumentationFile(rhetosBuildEnvironment).Load();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public DateTime GetLastModifiedTime()
        {
            if (!IsValidProjectRootPath(ProjectRootPath))
                throw new InvalidOperationException($"Can't load {nameof(DslSyntax)} from '{ProjectRootPath}', because it is not a valid project root path.");

            var filePath = Path.Combine(BuildCacheFolder(ProjectRootPath), DslSyntaxFile.DslSyntaxFileName);
            return new FileInfo(filePath).LastWriteTime;
        }

        public static bool IsValidProjectRootPath(string folder)
        {
            return File.Exists(Path.Combine(BuildCacheFolder(folder), DslSyntaxFile.DslSyntaxFileName));
        }

        private static string BuildCacheFolder(string projectFolder)
        {
            return Path.Combine(projectFolder, "obj", "Rhetos");
        }
    }
}
