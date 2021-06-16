using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;
using Rhetos.LanguageServices.CodeAnalysis.Tools;
using Rhetos.Utilities;

namespace Rhetos.LanguageServices.CommonTestTools
{
    public class DslSyntaxProviderMock : IDslSyntaxProvider
    {
        public string ProjectRootPath { get; }
        private DateTime lastModifiedTime;

        public DslSyntaxProviderMock(string explicitFolderWithDslSyntax, DateTime? lastModifiedTime = null)
        {
            ProjectRootPath = explicitFolderWithDslSyntax;
            this.lastModifiedTime = lastModifiedTime ?? DateTime.Now;
        }

        public DslSyntax Load()
        {
            return new DslSyntaxFile(new RhetosBuildEnvironment() {CacheFolder = ProjectRootPath}).Load();
        }

        public DateTime GetLastModifiedTime()
        {
            return lastModifiedTime;
        }
    }
}
