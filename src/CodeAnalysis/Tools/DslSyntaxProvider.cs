using System;
using System.IO;
using Rhetos.Dsl;
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.CodeAnalysis.Tools
{
    public class DslSyntaxProvider : IDslSyntaxProvider
    {
        public string ProjectRootPath { get; }

        public DslSyntaxProvider(string projectRootPath)
        {
            ProjectRootPath = projectRootPath;
        }

        public DslSyntax Load()
        {
            if (!IsValidProjectRootPath(ProjectRootPath))
                throw new InvalidOperationException($"Can't load {nameof(DslSyntax)} from '{ProjectRootPath}', because it is not a valid project root path.");

            var rhetosBuildEnvironment = new RhetosBuildEnvironment() { CacheFolder = BuildCacheFolder(ProjectRootPath) };
            return new DslSyntaxFile(rhetosBuildEnvironment).Load();
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
