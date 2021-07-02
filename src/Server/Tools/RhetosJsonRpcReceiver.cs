using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NLog;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.JsonRpc.Server.Messages;

namespace Rhetos.LanguageServices.Server.Tools
{
    public class RhetosJsonRpcReceiver : Receiver
    {
        public RhetosJsonRpcReceiver() : base()
        {
        }

        protected override Renor GetRenor(JToken @object)
        {
            JObject jObject = @object as JObject;
            if (jObject == null)
            {
                return new InvalidRequest(null, "Not an object");
            }
            //LogManager.GetLogger("GetRenor").Info(jObject.ToString());

            if (jObject["jsonrpc"]?.Value<string>() != "2.0")
            {
                return new InvalidRequest(null, "Unexpected protocol");
            }

            object id = null;
            bool flag;
            if (flag = jObject.TryGetValue("id", out JToken value))
            {
                object obj = (value.Type == JTokenType.String) ? ((string)value) : null;
                long? num = (value.Type == JTokenType.Integer) ? ((long?)value) : null;
                if (obj == null)
                {
                    obj = (num.HasValue ? ((object)num.Value) : null);
                }

                id = obj;
            }

            if (flag && jObject.TryGetValue("result", out JToken value2))
            {
                return new ServerResponse(id, value2);
            }

            if (jObject.TryGetValue("error", out JToken value3))
            {
                return new ServerError(id, value3.ToObject<ServerErrorResult>());
            }

            string text = jObject["method"]?.Value<string>();
            if (string.IsNullOrEmpty(text))
            {
                return new InvalidRequest(id, string.Empty, "Method not set");
            }

            if (jObject.TryGetValue("params", out JToken value4) && (value4 == null || value4.Type != JTokenType.Array) && (value4 == null || value4.Type != JTokenType.Object) && (value4 == null || value4.Type != JTokenType.Null))
            {
                return new InvalidRequest(id, text, "Invalid params");
            }

            if (value4 != null && value4.Type == JTokenType.Null)
            {
                value4 = new JObject();
            }

            ILookup<string, JProperty> lookup = jObject.Properties().ToLookup<JProperty, string>((JProperty z) => z.Name, StringComparer.OrdinalIgnoreCase);
            if (!flag)
            {
                return new Notification(text, value4)
                {
                    TraceState = lookup["tracestate"].FirstOrDefault()?.Value.Value<string>(),
                    TraceParent = lookup["traceparent"].FirstOrDefault()?.Value.Value<string>()
                };
            }

            return new Request(id, text, value4)
            {
                TraceState = lookup["tracestate"].FirstOrDefault()?.Value.Value<string>(),
                TraceParent = lookup["traceparent"].FirstOrDefault()?.Value.Value<string>()
            };
        }

        public override (IEnumerable<Renor> results, bool hasResponse) GetRequests(JToken container)
        {
            //LogManager.GetLogger("BLO").Info(container.ToString());

            return base.GetRequests(container);
        }

    }
}
