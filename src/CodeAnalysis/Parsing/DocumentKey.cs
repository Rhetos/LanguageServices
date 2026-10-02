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

namespace Rhetos.LanguageServices.CodeAnalysis.Parsing
{
    /// <summary>
    /// Identifies a document opened by the client.
    /// The <see cref="Uri"/> is kept exactly as supplied by the caller (the language server passes the canonical LSP document URI),
    /// so that diagnostics can be published with the same URI string as the opened document.
    /// The file-system path is supplied separately, because only the caller knows how to parse the URI correctly
    /// (for example, VS Code sends Windows drive letters as <c>file:///c%3A/...</c>).
    /// </summary>
    public sealed class DocumentKey
    {
        /// <summary>
        /// The document URI string, as supplied by the caller. Used as the document identifier in <see cref="Services.RhetosWorkspace"/>.
        /// </summary>
        public string Uri { get; }

        /// <summary>
        /// Fully qualified file-system path of the document, if the document is a file (<c>file</c> URI scheme).
        /// Null for other URI schemes, such as <c>untitled:</c>, which have no folder on disk.
        /// It is never a relative path, so a relative path cannot be resolved against the server's working directory.
        /// </summary>
        public string LocalPath { get; }

        /// <summary>
        /// True if the document is a file on disk (<see cref="LocalPath"/> is available).
        /// </summary>
        public bool IsFile => LocalPath != null;

        /// <summary>
        /// Creates a document key. See also <see cref="ForFile"/> and <see cref="ForNonFile"/>.
        /// </summary>
        /// <param name="uri">Document URI string. It must not be empty.</param>
        /// <param name="localPath">Fully qualified file-system path of the document (see <see cref="Path.IsPathFullyQualified(string)"/>),
        /// or null if the document is not a file.</param>
        /// <exception cref="ArgumentException">The URI is empty, or the local path is not null and not a fully qualified path.</exception>
        public DocumentKey(string uri, string localPath)
        {
            if (string.IsNullOrEmpty(uri))
                throw new ArgumentException("The document URI must not be empty.", nameof(uri));
            if (localPath != null && !Path.IsPathFullyQualified(localPath))
                throw new ArgumentException($"The local path must be null or a fully qualified path, but it is '{localPath}'.", nameof(localPath));

            Uri = uri;
            LocalPath = localPath;
        }

        /// <summary>
        /// Creates a key for a document that is a file on disk.
        /// </summary>
        public static DocumentKey ForFile(string uri, string localPath)
        {
            if (string.IsNullOrEmpty(localPath))
                throw new ArgumentException("The local path of a file document must not be empty.", nameof(localPath));

            return new DocumentKey(uri, localPath);
        }

        /// <summary>
        /// Creates a key for a document that is not a file on disk (for example <c>untitled:Untitled-1</c>).
        /// </summary>
        public static DocumentKey ForNonFile(string uri)
        {
            return new DocumentKey(uri, null);
        }

        /// <summary>
        /// Path used for the Rhetos DSL parser (<see cref="Rhetos.Dsl.DslScript.Path"/>). It is never empty:
        /// Rhetos reports error positions (<see cref="Rhetos.Dsl.DslSyntaxException.FilePosition"/>) only for scripts with a non-empty path.
        /// </summary>
        public string DslScriptPath => LocalPath ?? Uri;

        /// <inheritdoc/>
        public override string ToString() => Uri;
    }
}
