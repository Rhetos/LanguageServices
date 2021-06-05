using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;

namespace Rhetos.LanguageServices.CodeAnalysis.Tools
{
    public interface IDslSyntaxProvider
    {
        string ProjectRootPath { get; }
        DslSyntax Load();
    }
}
