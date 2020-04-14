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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.CodeContainerManagement;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Rhetos.LanguageServices.VisualStudioExtension
{
    public class SolutionRefreshService
    {
        private DTE2 dte;
        private Dictionary<string, DateTime?> beforeBuildSources;

        public async Task InitializeAsync(IAsyncServiceProvider provider, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            dte = (DTE2) await provider.GetServiceAsync(typeof(DTE));
            Assumes.Present(dte);
            dte.Events.BuildEvents.OnBuildBegin += BuildEvents_OnBuildBegin;
            dte.Events.BuildEvents.OnBuildDone += BuildEvents_OnBuildDone;
            await WriteToOutputWindowAsync(nameof(SolutionRefreshService), "Initialized!");
        }

        private void BuildEvents_OnBuildBegin(vsBuildScope scope, vsBuildAction action)
        {
            ThreadHelper.JoinableTaskFactory.Run(OnBuildBeginAsync);
        }

        private async Task OnBuildBeginAsync()
        {
            await WriteToOutputWindowAsync(nameof(SolutionRefreshService), "Build Begin!");
            beforeBuildSources = await EnumerateProjectSourcesAsync();
        }

        private void BuildEvents_OnBuildDone(vsBuildScope scope, vsBuildAction action)
        {
            ThreadHelper.JoinableTaskFactory.Run(OnBuildDoneAsync);
        }

        private async Task OnBuildDoneAsync()
        {
            await WriteToOutputWindowAsync(nameof(SolutionRefreshService), "Build Done!");
            var sources = await EnumerateProjectSourcesAsync();
            foreach (var source in sources)
            {
                if (beforeBuildSources == null
                    || !beforeBuildSources.ContainsKey(source.Key)
                    || beforeBuildSources[source.Key] != source.Value)
                {
                    await WriteToOutputWindowAsync(nameof(SolutionRefreshService), $"Project '{source.Key}' has changed, refreshing!");
                }
            }

            await RefreshProjectWindowAsync("ble");
        }

        // https://codeblog.vurdalakov.net/2016/11/vsix-get-list-of-projects-in-visual-studio-solution.html
        private async Task<Dictionary<string, DateTime?>> EnumerateProjectSourcesAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var result = new Dictionary<string, DateTime?>();
            foreach (var projectObject in dte.Solution.Projects)
            {
                var project = (EnvDTE.Project)projectObject;
                if (project.Kind != VSLangProj.PrjKind.prjKindCSharpProject)
                    continue;
                
                var projectName = project.FullName;

                result[projectName] = null;

                var folder = Path.GetDirectoryName(projectName);
                var rhetosSources = Path.Combine(folder, "obj", "Rhetos", "RhetosGeneratedSourceFiles.txt");
                if (File.Exists(rhetosSources))
                    result[projectName] = new FileInfo(rhetosSources).LastWriteTimeUtc;

                await WriteToOutputWindowAsync(nameof(SolutionRefreshService), $"{projectName}: {result[projectName]:s}");
            }

            return result;
        }

        private async Task RefreshProjectWindowAsync(string projectFullName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var solutionName = Path.GetFileNameWithoutExtension(dte.Solution.FullName);

            dte.Windows.Item(EnvDTE.Constants.vsWindowKindSolutionExplorer).Activate();

            foreach (var item in dte.ToolWindows.SolutionExplorer.UIHierarchyItems)
            {
                var pero = item;
            }
            
            dte.ToolWindows.SolutionExplorer.GetItem(solutionName + @"\" + "Rhetos40App2").Select(vsUISelectionType.vsUISelectionTypeSelect);
            dte.ExecuteCommand("View.Refresh", String.Empty);
            // Reactivate your old window
            //dte2.Windows.Item(captionOfActiveWindow).Activate();
        }

        private async Task RefreshSolutionAsync()
        {
            var cmd = "SolutionExplorer.Refresh";
            dte.ExecuteCommand(cmd);
            await WriteToOutputWindowAsync(nameof(SolutionRefreshService), $"Executed '{cmd}'.");
        }

        private async Task DebugCommandsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var allCommands = new List<EnvDTE.Command>();
            foreach (var item in dte.Commands)
            {
                allCommands.Add((EnvDTE.Command)item);
            }

            var refreshNames = allCommands
                .Select(a => a.Name)
                .Where(a => a.IndexOf("refresh", StringComparison.InvariantCultureIgnoreCase) >= 0);

            var all = string.Join("\n", refreshNames);

            await WriteToOutputWindowAsync(nameof(SolutionRefreshService), all);

            //var cmd = "SolutionExplorer.Refresh";
            var cmd = "ProjectandSolutionContextMenus.CrossProjectMultiItem.RefreshFolder";
            var refCommand = allCommands.Single(a => a.Name == cmd);
            dte.ExecuteCommand(cmd);
            await WriteToOutputWindowAsync(nameof(SolutionRefreshService), $"Executed '{cmd}'.");
        }

        private async Task WriteToOutputWindowAsync(string paneName, string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var panes = dte.ToolWindows.OutputWindow.OutputWindowPanes;
            OutputWindowPane pane;
            try
            {
                pane = panes.Item(paneName);
            }
            catch (ArgumentException)
            {
                pane = panes.Add(paneName);
            }

            pane.Activate();
            pane.OutputString(message);
            pane.OutputString(Environment.NewLine);
        }
    }
}
