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
            string vsixFolder = Path.Combine(installFolder, "VisualStudioExtension");
            var vsixs = new[]
            {
                (versionPrefix: "16.", vsixPath: Path.Combine(vsixFolder, "Rhetos.LanguageServices.VisualStudioExtension.vsix")),
                (versionPrefix: "17.", vsixPath: Path.Combine(vsixFolder, "Rhetos.LanguageServices.VisualStudioExtension17.vsix")),
            };

            try
            {
                var installedVersions = FindVisualStudioVersions();
                var compatibleVsixs = vsixs
                    .Where(vsix => installedVersions.Any(iv => iv.StartsWith(vsix.versionPrefix)))
                    .ToList();
                if (!compatibleVsixs.Any())
                    throw new InvalidOperationException("No compatible version of Visual Studio found by vswhere.exe."
                        + " Supported versions are " + string.Join(", ", vsixs.Select(vsix => $"'{vsix.versionPrefix}*'")) + ".");

                foreach (var compatibleVsixPath in compatibleVsixs.Select(vsix => vsix.vsixPath).Distinct())
                    InstallVsix(compatibleVsixPath);
            }
            catch (Exception e)
            {
                var message = $"echo Cannot find compatible version of Visual Studio: {e.Message}";
                message += $"& echo. & echo You might need to install the Visual Studio extension manually from '{vsixFolder}'.";
                message += $"& echo. & echo For additional help see https://github.com/Rhetos/LanguageServices.";
                message += "& echo.";
                Process.Start("cmd.exe", $"/c \"{message} & pause\"");
            }
        }

        private static ICollection<string> FindVisualStudioVersions()
        {
            string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
            if (!File.Exists(executable))
                throw new FileNotFoundException("Cannot find vswhere.exe.");

            ProcessStartInfo start = new ProcessStartInfo(executable)
            {
                Arguments = "-property catalog_productDisplayVersion",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            string output;
            using (var process = Process.Start(start))
            {
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
            }
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines;
        }

        private static void InstallVsix(string vsixPath)
        {
            try
            {
                var process = Process.Start(vsixPath);
                if (process == null)
                    throw new InvalidOperationException($"Unable to start installation for '{vsixPath}'.");
                process?.WaitForExit();
            }
            catch (Exception e)
            {
                var message = $"echo Error while trying to start Visual Studio Extension installation: {e.Message}";
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
