using System;
using System.IO;
using Rhetos.Dsl;
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.CodeAnalysis.Tools
{
    public class DslSyntaxProvider
    {
        private readonly string projectRootPath;

        public DslSyntaxProvider(string projectRootPath)
        {
            this.projectRootPath = projectRootPath;
        }

        public bool IsValidProjectRootPath()
        {
            return File.Exists(Path.Combine(CacheFolder(), DslSyntaxFile.DslSyntaxFileName));
        }

        public DslSyntax Load()
        {
            return LoadFromFolder(CacheFolder());
        }

        public static DslSyntax LoadFromFolder(string folder)
        {
            var rhetosBuildEnvironment = new RhetosBuildEnvironment() {CacheFolder = folder};
            return new DslSyntaxFile(rhetosBuildEnvironment).Load();
        }

        private string CacheFolder()
        {
            return Path.Combine(projectRootPath, "obj", "Rhetos");
        }
    }
}
