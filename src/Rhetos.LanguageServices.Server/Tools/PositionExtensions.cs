using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Rhetos.LanguageServices.Server.Tools;

namespace OmniSharp.Extensions.LanguageServer.Protocol.Models
{
    public static class PositionExtensions
    {
        public static LineChr ToLineChr(this Position position)
        {
            return new LineChr((int) position.Line, (int) position.Character);
        }

        public static Position ToPosition(this LineChr lineChr)
        {
            return new Position(lineChr.Line, lineChr.Chr);
        }
    }
}
