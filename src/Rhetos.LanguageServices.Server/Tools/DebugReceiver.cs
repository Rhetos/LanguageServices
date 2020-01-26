using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Rhetos.LanguageServices.Server.Tools
{
    public class DebugReceiver : ILspReciever
    {
        private readonly Logger _logger;
        private readonly LspReciever _default;

        public DebugReceiver()
        {
            _logger = LogManager.GetCurrentClassLogger();
            _default = new LspReciever();
        }

        public (IEnumerable<Renor> results, bool hasResponse) GetRequests(JToken container)
        {
            _logger.Info(container.ToString(Formatting.Indented));
            return _default.GetRequests(container);
        }

        public void Initialized()
        {
            _default.Initialized();
        }

        public bool IsValid(JToken container)
        {
            return _default.IsValid(container);
        }
    }
}
