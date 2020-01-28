using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rhetos.LanguageServices.Server.Services
{
    public class TrackedDocuments
    {
        private readonly Dictionary<string, string> documentContents = new Dictionary<string, string>();
        public TrackedDocuments()
        {
        }

        public void UpdateDocumentText(string id, string text)
        {
            documentContents[id] = text;
        }

        public string GetDocumentText(string id)
        {
            return documentContents[id];
        }
    }
}
