using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rhetos.LanguageServices.Server.Parsing;

namespace Rhetos.LanguageServices.Server.Services
{
    public class RhetosDocumentFactory
    {
        private readonly RhetosAppContext rhetosAppContext;
        private readonly ConceptQueries conceptQueries;
        private readonly ILoggerFactory logFactory;

        public RhetosDocumentFactory(RhetosAppContext rhetosAppContext, ConceptQueries conceptQueries, ILoggerFactory logFactory)
        {
            this.rhetosAppContext = rhetosAppContext;
            this.conceptQueries = conceptQueries;
            this.logFactory = logFactory;
        }

        public RhetosDocument CreateNew()
        {
            return new RhetosDocument(rhetosAppContext, conceptQueries, logFactory);
        }
    }
}
