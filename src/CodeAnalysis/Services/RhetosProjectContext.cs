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

using Microsoft.Extensions.Logging;
using Rhetos.Dsl;
using Rhetos.LanguageServices.CodeAnalysis.Parsing;
using Rhetos.LanguageServices.CodeAnalysis.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rhetos.LanguageServices.CodeAnalysis.Services
{
    public class RhetosProjectContext
    {
        public string ProjectRootPath => current?.DslSyntaxProvider.ProjectRootPath;
        /// <summary>
        /// Even if <see cref="IsInitialized"/> is <see langword="true"/>, also check <see cref="InitializationError"/> before using the context.
        /// The <see cref="current"/> context may be marked as "initialized" even if it has an InitializationError, in order to avoid
        /// retrying the failed initialization every second (see MonitorLoop) if the environment has not changed
        /// (Context.DslSyntaxLastModifiedTime or Context.DslSyntaxProvider.ProjectRootPath).
        /// </summary>
        public bool IsInitialized => current != null;
        public CodeAnalysisError InitializationError => current?.InitializationError;
        public DateTime LastContextUpdateTime => current?.CreatedTime ?? DateTime.MinValue;
        public DslSyntax DslSyntax => current?.DslSyntax;
        public Dictionary<string, ConceptType[]> Keywords => current?.Keywords;
        public Dictionary<string, ConceptDocumentation> Documentation => current?.DslDocumentation?.Concepts;

        private readonly ILogger<RhetosProjectContext> log;
        private readonly ILoggerFactory loggerFactory;
        private static readonly object _syncRoot = new();
        private Context current;

        private class Context
        {
            public IDslSyntaxProvider DslSyntaxProvider { get; }
            public DslSyntax DslSyntax { get; }
            public DslDocumentation DslDocumentation { get; }
            public DateTime DslSyntaxLastModifiedTime { get; }
            public Dictionary<string, ConceptType[]> Keywords { get; }
            public DateTime CreatedTime { get; }
            public CodeAnalysisError InitializationError { get; }

            public Context(IDslSyntaxProvider dslSyntaxProvider)
            {
                DslSyntaxProvider = dslSyntaxProvider;
                CreatedTime = DateTime.Now;
                DslSyntaxLastModifiedTime = dslSyntaxProvider.GetLastModifiedTime();

                var (dslSyntax, dslSyntaxError) = LoadDslSyntax(dslSyntaxProvider);
                if (dslSyntaxError == null)
                {
                    DslSyntax = dslSyntax;
                    DslDocumentation = dslSyntaxProvider.LoadDocumentation();
                    Keywords = ExtractKeywords(dslSyntax);
                }
                else
                {
                    InitializationError = new CodeAnalysisError { Message = dslSyntaxError };
                    DslDocumentation = new DslDocumentation { Concepts = new Dictionary<string, ConceptDocumentation>() };
                    Keywords = new Dictionary<string, ConceptType[]>();
                }
            }

            private static (DslSyntax Value, string Error) LoadDslSyntax(IDslSyntaxProvider dslSyntaxProvider)
            {
                var dslSyntax = dslSyntaxProvider.Load();

                string error = null;
                if (dslSyntax.Version == null)
                    error = $"Cannot detect the application's DSL syntax version (Rhetos {dslSyntax.RhetosVersion})." +
                        $" Currently installed Rhetos Language Services supports DSL version {DslSyntax.CurrentVersion}.";
                else if (dslSyntax.Version > DslSyntax.CurrentVersion)
                    error = $"Please install the latest version of Rhetos Language Services." +
                        $" The project uses a newer version of the DSL syntax: DSL version {dslSyntax.Version}, Rhetos {dslSyntax.RhetosVersion}." +
                        $" Currently installed Rhetos Language Services supports DSL version {DslSyntax.CurrentVersion} or lower.";

                return (dslSyntax, error);
            }
        }

        public RhetosProjectContext(ILogger<RhetosProjectContext> log, ILoggerFactory loggerFactory)
        {
            this.log = log;
            this.loggerFactory = loggerFactory;
        }

        public void Initialize(IDslSyntaxProvider dslSyntaxProvider)
        {
            lock (_syncRoot)
            {
                if (dslSyntaxProvider == null)
                    throw new ArgumentNullException(nameof(dslSyntaxProvider));

                if (string.IsNullOrEmpty(dslSyntaxProvider.ProjectRootPath))
                    throw new ArgumentNullException(nameof(dslSyntaxProvider.ProjectRootPath));

                if (ProjectRootPath == dslSyntaxProvider.ProjectRootPath && current.DslSyntaxLastModifiedTime == dslSyntaxProvider.GetLastModifiedTime())
                    throw new InvalidOperationException(
                        $"Trying to initialize with rootPath='{dslSyntaxProvider.ProjectRootPath}', but {nameof(RhetosProjectContext)} is already successfully initialized with same rootPath and same file DslSyntax timestamps.");

                current = new Context(dslSyntaxProvider);

                log.LogDebug($"Initialized with RootPath='{ProjectRootPath}'.");
            }
        }

        public void UpdateDslSyntax()
        {
            lock (_syncRoot)
            {
                if (!IsInitialized)
                    throw new InvalidOperationException($"Trying to update project context which is not initialized.");

                var dslSyntaxProvider = new DslSyntaxProvider(ProjectRootPath, new RhetosNetCoreLogProvider(loggerFactory));

                // project is no longer valid
                if (!DslSyntaxProvider.IsValidProjectRootPath(ProjectRootPath))
                {
                    current = null;
                    return;
                }

                if (current.DslSyntaxLastModifiedTime != dslSyntaxProvider.GetLastModifiedTime())
                    Initialize(dslSyntaxProvider);
            }
        }

        private static Dictionary<string, ConceptType[]> ExtractKeywords(DslSyntax dslSyntax)
        {
            if (dslSyntax?.ConceptTypes == null)
                throw new InvalidOperationException($"Provided DslSyntax.json is not valid. Property 'ConceptTypes' expected.");

            var keywordDictionary = dslSyntax.ConceptTypes
                .Select(type => (keyword: type.Keyword, type))
                .Where(info => !string.IsNullOrEmpty(info.keyword))
                .GroupBy(info => info.keyword)
                .ToDictionary(group => group.Key, group => group.Select(info => info.type).ToArray(), StringComparer.InvariantCultureIgnoreCase);

            return keywordDictionary;
        }
    }
}
