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
            foreach (var member in ConceptMembers.Get(type))
            {
                if (member.IsKey)
                    keys.Add(KeywordOrTypeName(member.ValueType));
                else
                    parameters.Add(member.ValueType.Name);
            }

            var keyword = KeywordOrTypeName(type);
            var keysDesc = string.Join(".", keys.Select(key => $"<{key}>"));
            var paramDesc = string.Join(" ", parameters.Select(parameter => $"<{parameter}>"));
            return $"{keyword} {keysDesc} {paramDesc}";
        }

        public static string KeywordOrTypeName(Type type)
        {
            var keyword = ConceptInfoHelper.GetKeyword(type);
            return string.IsNullOrEmpty(keyword)
                ? type.Name
                : keyword;
        }

        public static List<Type> ConceptInfoKeys(Type type)
        {
            var keys = new List<Type>();
            AddConceptInfoKeysRecursive(keys, type);
            return keys;
        }

        private static void AddConceptInfoKeysRecursive(List<Type> keys, Type type)
        {
            if (type != typeof(IConceptInfo))
            {
                var conceptMembers = ConceptMembers.Get(type);
                foreach (var conceptMember in conceptMembers)
                {
                    if (conceptMember.IsKey && conceptMember.IsConceptInfo && !keys.Contains(conceptMember.ValueType))
                        AddConceptInfoKeysRecursive(keys, conceptMember.ValueType);
                }
            }

            if (type.BaseType != null && type.BaseType != typeof(object)) type = type.BaseType;
            keys.Add(type);
        }
    }
}
