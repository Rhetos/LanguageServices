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

namespace Rhetos.LanguageServices.Server.Tools
{
    public static class ConceptInfoType
    {
        public static string SignatureDescription(Type type)
        {
            var keys = new List<string>();
            var parameters = new List<string>();
            foreach (var member in GetParameters(type))
            {
                if (member.IsKey)
                    keys.Add(ConceptMemberDescription(member));
                else
                    parameters.Add(ConceptMemberDescription(member));
            }

            var keyword = ConceptInfoHelper.GetKeywordOrTypeName(type);
            var keysDesc = string.Join(".", keys.Select(key => $"<{key}>"));
            var paramDesc = string.Join(" ", parameters.Select(parameter => $"<{parameter}>"));
            return $"{keyword} {keysDesc} {paramDesc}";
        }

        public static string ConceptMemberDescription(ConceptMember conceptMember)
        {
            return $"{conceptMember.Name}: {conceptMember.ValueType.Name}";
        }

        public static List<ConceptMember> GetParameters(Type conceptInfoType)
        {
            return ConceptMembers.Get(conceptInfoType)
                .Where(member => member.IsParsable)
                .ToList();
        }

        public static int IndexOfParameter(Type conceptInfoType, ConceptMember member)
        {
            var members = GetParameters(conceptInfoType);
            return members.IndexOf(member);
        }
    }
}
