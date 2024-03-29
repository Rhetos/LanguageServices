﻿/*
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using VSLangProj;
using VSLangProj80;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Rhetos.LanguageServices.VisualStudioExtension
{
    public class SolutionRefreshService
    {
        private DTE2 dte;
        private Dictionary<string, DateTime?> beforeBuildSources;
        private readonly string _outputName = "Rhetos DSL Language Extension";

        public async Task InitializeAsync(IAsyncServiceProvider provider, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            dte = (DTE2) await provider.GetServiceAsync(typeof(DTE));
            Assumes.Present(dte);
            dte.Events.BuildEvents.OnBuildBegin += BuildEvents_OnBuildBegin;
            dte.Events.BuildEvents.OnBuildDone += BuildEvents_OnBuildDone;
            await WriteToOutputWindowAsync(_outputName, "Initialized monitoring of Rhetos projects for source file changes.");
        }

        private void BuildEvents_OnBuildBegin(vsBuildScope scope, vsBuildAction action)
        {
            ThreadHelper.JoinableTaskFactory.Run(OnBuildBeginAsync);
        }

        private async Task OnBuildBeginAsync()
        {
            var projects = await GetProjectsAsync(dte.Solution);
            beforeBuildSources = await EnumerateProjectSourcesAsync(projects);
        }

        private void BuildEvents_OnBuildDone(vsBuildScope scope, vsBuildAction action)
        {
            ThreadHelper.JoinableTaskFactory.Run(OnBuildDoneAsync);
        }

        private async Task OnBuildDoneAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var oldActiveWindow = dte.ActiveWindow.Caption;

            var projects = await GetProjectsAsync(dte.Solution);
            var sources = await EnumerateProjectSourcesAsync(projects);
            foreach (var source in sources)
            {
                if (beforeBuildSources == null
                    || !beforeBuildSources.ContainsKey(source.Key)
                    || beforeBuildSources[source.Key] != source.Value)
                {
                    // HACK: Force the "design-time build", by updating timestamp on project.assets.json.
                    // DTB is needed to prevent the Visual Studio C# IntelliSense errors, because IntelliSense will not automatically detect the newly generated C# files.
                    string path = Path.Combine(Path.GetDirectoryName(source.Key), "obj", "project.assets.json");
                    if (SafeTouch(path))
                        await WriteToOutputWindowAsync(_outputName, $"'{source.Key}' has changed, refreshing project.");
                }
            }

            if (oldActiveWindow != dte.ActiveWindow.Caption)
                dte.Windows.Item(oldActiveWindow).Activate();
        }

        private async Task<Dictionary<string, DateTime?>> EnumerateProjectSourcesAsync(Project[] projects)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var result = new Dictionary<string, DateTime?>();
            foreach (var project in projects)
            {
                var projectName = project.FullName;

                result[projectName] = null;

                var folder = Path.GetDirectoryName(projectName);
                var rhetosSources = Path.Combine(folder, "obj", "Rhetos", "RhetosGeneratedSourceFiles.txt");
                if (File.Exists(rhetosSources))
                    result[projectName] = new FileInfo(rhetosSources).LastWriteTimeUtc;

                // await WriteToOutputWindowAsync(_outputName, $"{projectName}: {result[projectName]:s}");
            }

            return result;
        }

        public static async Task<Project[]> GetProjectsAsync(Solution solution)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var projects = new List<EnvDTE.Project>();

            var enumerator = solution.Projects.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var project = enumerator.Current as Project;
                projects.AddRange(await GetProjectsAsync(project));
            }

            return projects.ToArray();
        }

        private static async Task<Project[]> GetProjectsAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (project == null)
                return new Project[0];

            var projects = new List<EnvDTE.Project>();

            if (project.Kind == PrjKind.prjKindCSharpProject || project.Kind == PrjKind.prjKindVBProject)
            {
                projects.Add(project);
            }

            if (project.ProjectItems != null)
            {
                for (var i = 1; i <= project.ProjectItems.Count; i++)
                {
                    var subProject = project.ProjectItems.Item(i).SubProject;
                    projects.AddRange(await GetProjectsAsync(subProject));
                }
            }

            return projects.ToArray();
        }

        private async Task WriteToOutputWindowAsync(string paneName, string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var panes = dte.ToolWindows.OutputWindow.OutputWindowPanes;
            OutputWindowPane pane = null;
            try
            {
                pane = panes.Item(paneName);
            }
            catch (ArgumentException)
            {
            }

            if (pane == null)
            {
                try
                {
                    pane = panes.Item("Build");
                }
                catch (ArgumentException)
                {
                }
            }

            // if neither panes exist, return without outputting anything
            if (pane == null) return;

            pane.OutputString($"{nameof(SolutionRefreshService)}: {message}{Environment.NewLine}");
        }

        public static bool SafeTouch(string path)
        {
            var file = new FileInfo(path);
            if (file.Exists)
            {
                var isReadOnly = file.IsReadOnly;
                file.IsReadOnly = false;
                file.LastWriteTime = DateTime.Now;
                file.IsReadOnly = isReadOnly;
                return true;
            }
            else
                return false;
        }
    }
}
