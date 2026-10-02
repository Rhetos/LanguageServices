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
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;

namespace Rhetos.LanguageServices.Server.Tools
{
    /// <summary>
    /// Converts OmniSharp's <see cref="DocumentUri"/> to the protocol-independent <see cref="DocumentKey"/> used by CodeAnalysis.
    /// </summary>
    internal static class DocumentKeyExtensions
    {
        /// <summary>
        /// The key's URI is <see cref="DocumentUri.ToString()"/>, the canonical form that OmniSharp also writes to the wire when publishing
        /// diagnostics (for example, VS Code's <c>file:///c%3A/a.rhe</c> becomes <c>file:///c:/a.rhe</c>), so published URIs equal the opened URI.
        /// The local path is taken from <see cref="Uri.LocalPath"/> of <see cref="DocumentUri.ToUri()"/>, which decodes the path correctly
        /// and keeps the drive letter case sent by the client (<see cref="DocumentUri.GetFileSystemPath()"/> would lowercase it).
        /// </summary>
        public static DocumentKey ToDocumentKey(this DocumentUri documentUri, ILogger log = null)
        {
            ArgumentNullException.ThrowIfNull(documentUri);

            var uri = documentUri.ToString();
            if (!string.Equals(documentUri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
                return DocumentKey.ForNonFile(uri);

            string localPath;
            try
            {
                localPath = documentUri.ToUri().LocalPath;
            }
            catch (Exception e) when (e is UriFormatException || e is InvalidOperationException || e is ArgumentException)
            {
                log?.LogDebug($"Cannot get the local path of document '{uri}', it will be handled as a non-file document. {e.Message}");
                return DocumentKey.ForNonFile(uri);
            }

            if (string.IsNullOrEmpty(localPath) || !Path.IsPathFullyQualified(localPath))
            {
                log?.LogDebug($"The local path '{localPath}' of document '{uri}' is not an absolute path, the document will be handled as a non-file document.");
                return DocumentKey.ForNonFile(uri);
            }

            return DocumentKey.ForFile(uri, localPath);
        }
    }
}
