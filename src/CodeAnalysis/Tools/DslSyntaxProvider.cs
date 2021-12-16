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
