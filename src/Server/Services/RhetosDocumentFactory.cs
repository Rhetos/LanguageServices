/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
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

        public RhetosDocument CreateNew(Uri documentUri)
        {
            return new RhetosDocument(rhetosAppContext, conceptQueries, logFactory, documentUri);
        }
    }
}
