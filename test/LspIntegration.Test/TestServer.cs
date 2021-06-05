using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;

namespace Rhetos.LanguageServices.LspIntegration.Test
{
    public class TestServer : LanguageProtocolTestBase
    {
        public TestServer(JsonRpcTestOptions testOptions) : base(testOptions) { }
    }
}
