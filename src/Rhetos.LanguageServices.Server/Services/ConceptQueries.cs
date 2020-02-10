using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhetos.Dsl;
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
                
            var fullDescription = string.Join("\n\n", signatures.Select(sig => $"{sig.signature}\n{sig.documentation}"));
            return fullDescription;
        }

        public List<(Type conceptInfoType, string signature, string documentation)> GetSignaturesWithDocumentation(string keyword)
        {
            if (string.IsNullOrEmpty(keyword) || !rhetosAppContext.Keywords.TryGetValue(keyword, out var keywordTypes))
                return null;

            var prefix = "    ";
            var signatures = keywordTypes
                .Select(type =>
                {
                    var signature = ConceptInfoType.SignatureDescription(type);
                    var documentation = $"{prefix}Defined by {type.Name}";
                    var xmlDocumentation = xmlDocumentationProvider.GetDocumentation(type, prefix);
                    if (!string.IsNullOrEmpty(xmlDocumentation)) documentation += $"\n{xmlDocumentation}";
                    return (type, signature, documentation);
                });

            return signatures.ToList();
        }

        public List<Type> ValidConceptsForParent(Type parentConceptInfoType)
        {
            var result = new List<Type>();
            var parentKeys = ConceptInfoType.ConceptInfoKeys(parentConceptInfoType);
            foreach (var conceptType in rhetosAppContext.ConceptInfoTypes)
            {
                if (conceptType == parentConceptInfoType) continue;
                var conceptKeys = ConceptInfoType.ConceptInfoKeys(conceptType);
                if (StartsWithEquivalentConceptTypes(conceptKeys, parentKeys)) result.Add(conceptType);
            }

            return result;
        }
        
        // TODO: SqlDependsOnID ??
        private bool StartsWithEquivalentConceptTypes(List<Type> list, List<Type> subList)
        {
            if (subList.Count > list.Count) return false;

            for (var i = 0; i < subList.Count; i++)
            {
                if (list[i] != subList[i]) return false;
            }

            return true;
        }
    }
}
