using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
