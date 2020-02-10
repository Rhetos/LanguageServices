using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Rhetos.LanguageServices.Server.Services
{
    public class XmlDocumentationProvider
    {
        private readonly ConcurrentDictionary<string, Lazy<XDocument>> assemblyDocuments = new ConcurrentDictionary<string, Lazy<XDocument>>();
        private readonly ConcurrentDictionary<Type, Lazy<string>> typeDocumentation = new ConcurrentDictionary<Type, Lazy<string>>();
        private readonly ILogger<XmlDocumentationProvider> log;

        public XmlDocumentationProvider(ILogger<XmlDocumentationProvider> log)
        {
            this.log = log;
        }

        public string GetDocumentation(Type type, string linePrefix = null)
        {
            var documentation = typeDocumentation.GetOrAdd(type, key => new Lazy<string>(() => LoadDocumentation(key)))
                .Value;

            if (!string.IsNullOrEmpty(documentation) && !string.IsNullOrEmpty(linePrefix))
                documentation = linePrefix + documentation.Replace("\n", $"\n{linePrefix}");

            return documentation;
        }

        private string LoadDocumentation(Type type)
        {
            try
            {
                var codebase = new Uri(type.Assembly.CodeBase).AbsolutePath;
                var xDocument = assemblyDocuments.GetOrAdd(codebase, key => new Lazy<XDocument>(() => LoadXmlDocumentForAssembly(key)));

                var typeKey = $"T:{type.FullName}";
                var typeInfo = xDocument.Value
                    .Descendants(XName.Get("member"))
                    .SingleOrDefault(member => member.Attribute(XName.Get("name"))?.Value == typeKey);

                var summary = typeInfo?
                    .Descendants(XName.Get("summary"))
                    .SingleOrDefault()
                    ?.Value
                    ?.Trim();

                if (!string.IsNullOrEmpty(summary))
                {
                    var lines = summary.Split(new[] {"\r\n", "\n"}, StringSplitOptions.None);
                    summary = string.Join("\n", lines.Select(line => line.Trim()));
                }
                return summary;
            }
            catch (Exception e)
            {
                log.LogInformation($"Error getting summary from XML document for '{type.AssemblyQualifiedName}'. {e}");
                return null;
            }
        }

        private XDocument LoadXmlDocumentForAssembly(string codeBasePath)
        {
            var xDocument = new XDocument();
            try
            {
                var directory = Path.GetDirectoryName(codeBasePath);
                var filename = Path.GetFileNameWithoutExtension(codeBasePath);
                var xmlPath = Path.Combine(directory, $"{filename}.xml");

                if (File.Exists(xmlPath))
                {
                    xDocument = XDocument.Parse(File.ReadAllText(xmlPath));
                    log.LogDebug($"Loaded XML documentation from '{xmlPath}'.");
                }
                else
                {
                    log.LogDebug($"No XML documentation found at '{xmlPath}'.");
                }
            }
            catch (Exception e)
            {
                log.LogInformation($"Failed to load XML documentation for assembly '{codeBasePath}'. {e}");
            }

            return xDocument;
        }
    }
}
