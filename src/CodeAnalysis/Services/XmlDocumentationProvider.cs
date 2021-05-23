/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Rhetos.Dsl;

namespace Rhetos.LanguageServices.CodeAnalysis.Services
{
    public class XmlDocumentationProvider
    {
        private readonly ConcurrentDictionary<string, Lazy<XDocument>> assemblyDocuments = new ConcurrentDictionary<string, Lazy<XDocument>>();
        private readonly ConcurrentDictionary<ConceptType, Lazy<string>> typeDocumentation = new ConcurrentDictionary<ConceptType, Lazy<string>>();
        private readonly ILogger<XmlDocumentationProvider> log;

        public XmlDocumentationProvider(ILogger<XmlDocumentationProvider> log)
        {
            this.log = log;
        }

        public string GetDocumentation(ConceptType conceptType, string linePrefix = null)
        {
            var documentation = typeDocumentation.GetOrAdd(conceptType, key => new Lazy<string>(() => LoadDocumentation(key)))
                .Value;

            if (!string.IsNullOrEmpty(documentation) && !string.IsNullOrEmpty(linePrefix))
                documentation = linePrefix + documentation.Replace("\n", $"\n{linePrefix}");

            return documentation;
        }

        private string LoadDocumentation(ConceptType conceptType)
        {
            return "Missing documentation from ConceptType class.";
            /*
            try
            {
                var codebase = new Uri(type.Assembly.CodeBase).LocalPath;
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
            */
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
