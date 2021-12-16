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
