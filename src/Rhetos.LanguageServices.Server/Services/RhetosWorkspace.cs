using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NLog;
using Rhetos.LanguageServices.Server.Parsing;
using Rhetos.LanguageServices.Server.Tools;
using Rhetos.Logging;
using Rhetos.Utilities.ApplicationConfiguration;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Rhetos.LanguageServices.Server.Services
{
    public class RhetosWorkspace
    {
        public Dictionary<string, DateTime> DocumentChangeTimes { get; } = new Dictionary<string, DateTime>();

        private readonly Dictionary<string, RhetosDocument> rhetosDocuments = new Dictionary<string, RhetosDocument>();
        private readonly ILogger<RhetosWorkspace> log;
        private readonly RhetosDocumentFactory rhetosDocumentFactory;

        public RhetosWorkspace(RhetosDocumentFactory rhetosDocumentFactory, ILogger<RhetosWorkspace> log)
        {
            this.log = log;
            this.rhetosDocumentFactory = rhetosDocumentFactory;
        }

        public void UpdateDocumentText(Uri documentUri, string text)
            => UpdateDocumentText(documentUri.AbsoluteUri, text);

        public void UpdateDocumentText(string id, string text)
        {
            var rhetosDocument = GetRhetosDocument(id);
            if (rhetosDocument == null)
            {
                rhetosDocument = rhetosDocumentFactory.CreateNew();
                rhetosDocuments.Add(id, rhetosDocument);
            }

            rhetosDocument.UpdateText(text);
            DocumentChangeTimes[id] = DateTime.Now;
        }

        public RhetosDocument GetRhetosDocument(Uri documentUri)
            => GetRhetosDocument(documentUri.AbsoluteUri);

        public RhetosDocument GetRhetosDocument(string id)
        {
            if (!rhetosDocuments.TryGetValue(id, out var rhetosDocument))
                return null;

            return rhetosDocument;
        }

        public RootPathConfiguration GetRhetosAppRootPath(Uri documentUri)
        {
            var rhetosDocument = GetRhetosDocument(documentUri);
            if (rhetosDocument == null)
                throw new InvalidOperationException($"No document with id='{documentUri}' found.");

            var fromDirective = GetRhetosAppRootPathFromText(rhetosDocument.TextDocument.Text);
            if (fromDirective != null)
                return new RootPathConfiguration(fromDirective, RootPathConfigurationType.SourceDirective, documentUri.LocalPath);
            
            var fromDetected = FindRhetosAppRootPathInParentFolders(Path.GetDirectoryName(documentUri.LocalPath));

            return fromDetected;
        }

        private string GetRhetosAppRootPathFromText(string text)
        {
            log.LogInformation($"Checking for 'rhetosAppRootPath' directive in document source.");
            var pathMatch = Regex.Match(text, @"^\s*//\s*<rhetosAppRootPath=""(.+)""\s*/>");
            var rootPath = pathMatch.Success
                ? pathMatch.Groups[1].Value
                : null;

            return rootPath;
        }

        private RootPathConfiguration FindRhetosAppRootPathInParentFolders(string startingFolder)
        {
            var folder = Path.GetFullPath(startingFolder);
            while (Directory.Exists(folder))
            {
                log.LogInformation($"Checking '{folder}' for RhetosAppRoot.");
                if (RhetosAppEnvironmentProvider.IsRhetosApplicationRootFolder(folder))
                    return new RootPathConfiguration(folder, RootPathConfigurationType.DetectedRhetosApp, folder);

                var parent = Path.GetFullPath(Path.Combine(folder, ".."));
                if (parent == folder) break;
                folder = parent;
            }

            return null;
        }
    }
}
