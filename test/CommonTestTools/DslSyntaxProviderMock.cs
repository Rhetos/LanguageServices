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

        public DslSyntaxProviderMock(string explicitFolderWithDslSyntax)
        {
            ProjectRootPath = explicitFolderWithDslSyntax;
        }

        public DslSyntax Load()
        {
            return new DslSyntaxFile(new RhetosBuildEnvironment() {CacheFolder = ProjectRootPath}).Load();
        }
    }
}
