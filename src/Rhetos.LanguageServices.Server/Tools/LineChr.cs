using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rhetos.LanguageServices.Server.Tools
{
    public struct LineChr
    {
        public readonly int Line;
        public readonly int Chr;

        public static readonly LineChr Zero = new LineChr(0, 0);

        public LineChr(int line, int chr)
        {
            this.Line = line;
            this.Chr = chr;
        }

        public override string ToString()
        {
            return $"({Line}, {Chr})";
        }
    }
}
