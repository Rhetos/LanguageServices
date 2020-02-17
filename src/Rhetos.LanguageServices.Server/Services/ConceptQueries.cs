using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var documentation = $"{prefix}Defined by {conceptInfoType.Name}";
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
                var firstMember = ConceptMembers.Get(conceptType).FirstOrDefault();
                if (firstMember == null || !firstMember.IsKey || !firstMember.IsConceptInfo) continue;

                if (firstMember.ValueType.IsAssignableFrom(parentConceptInfoType))
                    result.Add(conceptType);
            }

            return result;
        }
    }
}
