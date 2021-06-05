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
