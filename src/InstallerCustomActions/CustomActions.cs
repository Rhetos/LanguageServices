using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace InstallerCustomActions
{
    [RunInstaller(true)]
    public partial class CustomActions : System.Configuration.Install.Installer
    {
        private static readonly string _registryKeyPath = "SOFTWARE\\Rhetos\\RhetosLanguageServices";
        public CustomActions()
        {
            InitializeComponent();
        }

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);

            var installFolder = GetInstallFolder();
            var vsixPath = Path.Combine(installFolder, "VisualStudioExtension", "Rhetos.LanguageServices.VisualStudioExtension.vsix");

            try
            {
                var process = Process.Start(vsixPath);
                if (process == null)
                    throw new InvalidOperationException($"Unable to start installation for '{vsixPath}'.");
                process?.WaitForExit();
            }
            catch (Exception e)
            {
                var message = $"echo Error while trying to start Visual Studio Extension installation: {e.Message}.";
                message += $"& echo. & echo You might need to install the extension manually from '{vsixPath}'.";
                message += $"& echo. & echo For additional help see https://github.com/Rhetos/LanguageServices.";
                message += "& echo.";
                Process.Start("cmd.exe", $"/c \"{message} & pause\"");
            }
        }

        public override void Commit(IDictionary savedState)
        {
            base.Commit(savedState);

            var subKey = Registry.LocalMachine.CreateSubKey(_registryKeyPath);
            subKey.SetValue("Location", GetInstallFolder());
            subKey.Close();
        }

        public override void Uninstall(IDictionary savedState)
        {
            base.Uninstall(savedState);

            Registry.LocalMachine.DeleteSubKey(_registryKeyPath, false);
        }

        private string GetInstallFolder()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
    }
}
