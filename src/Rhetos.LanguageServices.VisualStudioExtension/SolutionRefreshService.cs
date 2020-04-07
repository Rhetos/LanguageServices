using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Rhetos.LanguageServices.VisualStudioExtension
{
    public class SolutionRefreshService
    {
        private DTE2 dte;

        public async Task InitializeAsync(IAsyncServiceProvider provider, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            dte = (DTE2) await provider.GetServiceAsync(typeof(DTE));
            Assumes.Present(dte);
            dte.Events.BuildEvents.OnBuildDone += BuildEvents_OnBuildDone;
            await WriteToOutputWindowAsync(nameof(SolutionRefreshService), "Initialized!");
        }

        private void BuildEvents_OnBuildDone(vsBuildScope scope, vsBuildAction action)
        {
            ThreadHelper.JoinableTaskFactory.Run(() => WriteToOutputWindowAsync(nameof(SolutionRefreshService), "Build Done!"));
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
