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

namespace Rhetos.LanguageServices.CodeAnalysis.Parsing
{
    public static class ConceptTypeTools
    {
        public static string SignatureDescription(ConceptType conceptType)
        {
            var keys = new List<string>();
            var parameters = new List<string>();
            foreach (var member in GetParameters(conceptType))
            {
                if (member.IsKey)
                    keys.Add(ConceptMemberDescription(member));
                else
                    parameters.Add(ConceptMemberDescription(member));
            }

            var keyword = conceptType.GetKeywordOrTypeName();
            var keysDesc = string.Join(".", keys.Select(key => $"<{key}>"));
            var paramDesc = string.Join(" ", parameters.Select(parameter => $"<{parameter}>"));
            return $"{keyword} {keysDesc} {paramDesc}";
        }

        public static string ConceptMemberDescription(ConceptMemberSyntax conceptMember)
        {
            return $"{conceptMember.Name}: {ConceptMemberValueType(conceptMember)}";
        }

        public static string ConceptMemberValueType(ConceptMemberSyntax conceptMember)
        {
            if (conceptMember.IsStringType)
                return "String";

            if (conceptMember.IsConceptInfoInterface)
                return "IConceptInfo";

            return conceptMember.ConceptType.TypeName;
        }

        public static List<ConceptMemberSyntax> GetParameters(ConceptType conceptType)
        {
            return conceptType.Members
                .Where(member => member.IsParsable)
                .ToList();
        }

        public static int IndexOfParameter(ConceptType conceptType, ConceptMemberSyntax conceptMember)
        {
            var members = GetParameters(conceptType);
            return members.IndexOf(conceptMember);
        }
    }
}
