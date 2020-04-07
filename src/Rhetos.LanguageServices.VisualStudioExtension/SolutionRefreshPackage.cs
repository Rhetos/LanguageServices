using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace Rhetos.LanguageServices.VisualStudioExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("My Asynchronous Package", "Loads asynchronously", "1.0")]
    [ProvideService(typeof(SolutionRefreshService), IsAsyncQueryable = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid("c4ddd728-d28b-49fe-bdd9-fdfd590d4391")]
    public sealed class SolutionRefreshPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // Adds a service on the background thread
            AddService(typeof(SolutionRefreshService), CreateMyServiceAsync);

            var svc = await GetServiceAsync(typeof(SolutionRefreshService));
        }

        private async Task<object> CreateMyServiceAsync(IAsyncServiceContainer container, CancellationToken cancellationToken, Type serviceType)
        {
            var svc = new SolutionRefreshService();
            await svc.InitializeAsync(this, cancellationToken);
            return svc;
        }
    }
}
