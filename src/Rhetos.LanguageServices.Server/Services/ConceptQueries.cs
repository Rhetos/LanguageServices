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
using System.Collections.Generic;
using System.Linq;
using Rhetos.Dsl;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Tools;

namespace Rhetos.LanguageServices.Server.Services
{
    public class ConceptQueries
    {
        private readonly RhetosAppContext rhetosAppContext;
        private readonly XmlDocumentationProvider xmlDocumentationProvider;

        public ConceptQueries(RhetosAppContext rhetosAppContext, XmlDocumentationProvider xmlDocumentationProvider)
        {
            this.rhetosAppContext = rhetosAppContext;
            this.xmlDocumentationProvider = xmlDocumentationProvider;
        }

        public string GetFullDescription(string keyword)
        {
            var signatures = GetSignaturesWithDocumentation(keyword);
            if (signatures == null) return null;

            var fullDescription = string.Join("\n\n", signatures.Select(sig => $"{sig.Signature}\n{sig.Documentation}"));
            return fullDescription;
        }

        public List<RhetosSignature> GetSignaturesWithDocumentation(string keyword)
        {
            if (string.IsNullOrEmpty(keyword) || !rhetosAppContext.Keywords.TryGetValue(keyword, out var keywordTypes))
                return null;

            var signatures = keywordTypes.Select(CreateRhetosSignature);
            return signatures.ToList();
        }

        private RhetosSignature CreateRhetosSignature(Type conceptInfoType)
        {
            var prefix = "    ";

            var signature = ConceptInfoType.SignatureDescription(conceptInfoType);
            var documentation = $"{prefix}Defined by {conceptInfoType.FullName}";
            var xmlDocumentation = xmlDocumentationProvider.GetDocumentation(conceptInfoType, prefix);
            if (!string.IsNullOrEmpty(xmlDocumentation)) documentation += $"\n{xmlDocumentation}";

            return new RhetosSignature()
            {
                ConceptInfoType = conceptInfoType,
                Parameters = ConceptInfoType.GetParameters(conceptInfoType),
                Signature = signature,
                Documentation = documentation
            };
        }

        public List<Type> ValidConceptsForParent(Type parentConceptInfoType)
        {
            var result = new List<Type>();
            foreach (var conceptType in rhetosAppContext.ConceptInfoTypes)
            {
                if (conceptType == parentConceptInfoType) continue;

                var members = ConceptMembers.Get(conceptType);
                var parentNestedMember = members.FirstOrDefault(member => member.IsParentNested);
                var firstMember = members.FirstOrDefault();

                // is first member valid?
                if (firstMember == null || !firstMember.IsKey || !firstMember.IsConceptInfo)
                    firstMember = null;

                var parentMember = parentNestedMember ?? firstMember;
                if (parentMember == null) continue;

                if (parentMember.ValueType.IsAssignableFrom(parentConceptInfoType))
                    result.Add(conceptType);
            }

            return result;
        }
    }
}
