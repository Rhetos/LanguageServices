using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Tools;

namespace Rhetos.LanguageServices.CodeAnalysis.Services
{
    public interface IRhetosProjectRootPathResolver
    {
        RootPathConfiguration ResolveRootPathFromDocumentDirective(RhetosDocument rhetosDocument);
        RootPathConfiguration ResolveRootPathForDocument(RhetosDocument rhetosDocument);
    }
}
