using System.ComponentModel.Composition;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;

namespace Rhetos.LanguageServices.VisualStudioExtension
{
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null
    public static class RheContentDefinition
    {
        [Export]
        [Name("rhe")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition RheContentTypeDefinition;

        [Export]
        [FileExtension(".rhe")]
        [ContentType("rhe")]
        internal static FileExtensionToContentTypeDefinition RheFileExtensionDefinition;
    }
#pragma warning restore CS0649
}
