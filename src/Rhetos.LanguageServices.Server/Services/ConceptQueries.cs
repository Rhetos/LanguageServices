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

        public ConceptQueries(RhetosAppContext rhetosAppContext)
        {
            this.rhetosAppContext = rhetosAppContext;
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
