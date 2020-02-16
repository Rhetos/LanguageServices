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

        public static List<Type> ConceptInfoKeys(Type type)
        {
            var keys = new List<Type>();
            AddConceptInfoKeysRecursive(keys, type);
            return keys;
        }

        public static IConceptInfo MemberwiseClone(IConceptInfo concept)
        {
            var members = ConceptMembers.Get(concept);
            var clone = (IConceptInfo)Activator.CreateInstance(concept.GetType());
            foreach (var member in members)
                member.SetMemberValue(clone, member.GetValue(concept));

            return clone;
        }

        public static List<ConceptMember> GetParameters(Type conceptInfoType)
        {
            return ConceptMembers.Get(conceptInfoType)
                .Where(member => member.IsParsable)
                .ToList();
        }

        public static int CountNonNullParsableMembers(IConceptInfo concept)
        {
            var count = 0;
            foreach (var member in GetParameters(concept.GetType()))
            {
                if (member.GetValue(concept) == null) break;
                count++;
            }

            return count;
        }

        public static int IndexOfParameter(Type conceptInfoType, ConceptMember member)
        {
            var members = GetParameters(conceptInfoType);
            return members.IndexOf(member);
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
