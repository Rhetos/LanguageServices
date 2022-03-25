# Rhetos Language Services

This project is a [Language Server Protocol](https://microsoft.github.io/language-server-protocol/) implementation for Rhetos DSL. It includes a language server and a corresponding Visual Studio extension.
It supports applications with Rhetos v4 and higher.

Contents:

1. [Features](#features)
2. [Installation](#installation)
3. [Usage](#usage)
4. [Limitations](#limitations)
   1. [Changing Rhetos application after initialization](#changing-rhetos-application-after-initialization)
   2. [Syntax highlight keywords](#syntax-highlight-keywords)
   3. [Documents analyzed](#documents-analyzed)
5. [How to contribute](#how-to-contribute)
   1. [Building and testing the source code](#building-and-testing-the-source-code)
   2. [Releasing a new version](#releasing-a-new-version)
6. [Troubleshooting](#troubleshooting)
   1. [Rhetos application not detected after opening `.rhe` file](#rhetos-application-not-detected-after-opening-rhe-file)
   2. [Diagnostics and advanced troubleshooting](#diagnostics-and-advanced-troubleshooting)

## Features

1. DSL syntax highlighting
2. DSL script parse error reporting
3. Keyword autocompletion
4. Parameters autocompletion based on all tokens in the current document
5. Signature help for concept parameters
6. Keyword and signature info on hover
7. XML-style documentation from IConceptInfo implementation classes
8. Automatic refresh of Rhetos projects when Rhetos generated source files change (this refresh is needed to update C# IntelliSense)

## Installation

Before installing a new version, **uninstall the old version first:**

1. In Visual Studio under Extensions uninstall "Rhetos DSL Language Extension".
2. In Windows "Apps & features" uninstall "RhetosLanguageServices".

Setup:

1. Install by downloading and running latest version of *RhetosLanguageServicesInstaller.msi* from <https://github.com/Rhetos/LanguageServices/releases>.
2. Applications with **Rhetos v4** additionally need to install [Rhetos.LanguageServicesCompatibility](https://github.com/Rhetos/LanguageServicesCompatibility) NuGet package.
  **Rhetos v5** apps, and newer, support Rhetos Language Services by default; they do not need the LanguageServicesCompatibility plugin.

## Usage

Rhetos Language Services will activate whenever `.rhe` file is opened within Visual Studio. An attempt to detect a corresponding Rhetos application is made and if successful, the system will provide IntelliSense and other features to the editor.

If your `.rhe` document resides in the directory tree inside your Rhetos application, it will automatically be detected by rule (3), so no additional configuration is needed.

For a `.rhe` document, the following rules are applied in order to find the location of corresponding Rhetos application:

1. Document is checked for explicit directive pointing to a folder which contains Rhetos application in the form of: `// <rhetosProjectRootPath="c:\some\path\to\rhetosproject" />`. It must be placed on the first line of the file.

2. Document folder and all parent folders are checked for `rhetos-language-services.settings.json` configuration file specifying the Rhetos application path. File format should be:

    ``` json
    {
        "RhetosProjectRootPath": "c:\\some\\path\\to\\rhetosproject"
    }
    ```

3. Document folder and all parent folders are checked for a valid Rhetos application root path. This generally means that folders are checked for existence of the `obj/Rhetos/rhetos-project.assets.json` file which is produced by building the Rhetos application.

Use rules (1) and (2) as means to override this default behavior.

## Limitations

### Changing Rhetos application after initialization

Initializing language services server with specific Rhetos application causes `.dll` files of that application to be loaded (for DSL concept discovery). As a consequence, once initialized with a specific Rhetos application, services will only work for that application for the duration of the process runtime.

To change the Rhetos application used in code analysis, restart Visual Studio.

### Syntax highlight keywords

Syntax highlighting has a fixed set of keywords being recognized, independent of the Rhetos application currently used.

### Documents analyzed

Language services will analyze and report errors only on **currently opened documents** in the IDE.

## How to contribute

Contributions are very welcome. The easiest way is to fork this repo, and then
make a pull request from your fork. The first time you make a pull request, you
may be asked to sign a Contributor Agreement.

For more info see [How to Contribute](https://github.com/Rhetos/Rhetos/wiki/How-to-Contribute) on Rhetos wiki.

### Building and testing the source code

Open Rhetos.LanguageServices.sln in Visual Studio 2019, build the solution and run the unit tests. **Prerequisites**:

* If missing, Visual Studio will automatically offer to install the required component "Visual Studio extension development".
* If Visual Studio cannot open RhetosLanguageServicesInstaller project, displaying "(incompatible)" in Solution Explorer, in Visual Studio install extension "Microsoft Visual Studio Installer Projects".

Build will produce installation file `RhetosLanguageServicesInstaller.msi` in `src\RhetosLanguageServicesInstaller\Debug`.
It contains the Visual Studio extension for DSL IntelliSense and LSP server.
See the [Installation instructions](#installation) above.

Automating the build with `Build.bat` has known issues, and is currently not available.

### Releasing a new version

For example, for version `2.0.3`.

1. If building a prerelease for internal testing, run: `powershell .\tools\ChangeVersion.ps1 2.0.3 auto`
   * If building a public release run: `powershell .\tools\ChangeVersion.ps1 2.0.3`,
     and update ChangeLog.md file.
2. `clean.bat`
3. Open Rhetos.LanguageServices.sln in Visual Studio 2019
   1. Rebuild the solution.
   2. Run unit tests.
4. `powershell .\tools\ChangeVersion.ps1 2.0.3 dev`
5. The installation package is created in `src\RhetosLanguageServicesInstaller\Debug\RhetosLanguageServicesInstaller.msi`
6. If building a public release:
   * Add git tag to the current commit and push tags to GitGub.
   * Create a new release on [GitHub](https://github.com/Rhetos/LanguageServices/releases)
     for the tag, and upload the MSI file under Assets.
   * Prepare development for the next minor version with `powershell .\tools\ChangeVersion.ps1 2.1.0 dev`

## Troubleshooting

### Rhetos application not detected after opening `.rhe` file

Make sure you have built Rhetos application at least once.

### Diagnostics and advanced troubleshooting

If the Rhetos Language Services server starts correctly (after opening any `.rhe` file) it will log some basic information in the Visual Studio "Output" window under "Rhetos DSL Language Extension" source.

The Output window will display the **location from where Rhetos Language Services server is running** as well as additional **log file path** with detailed logging (if configured).

Use the log file for advanced troubleshooting in case of problems/errors.
Configure logging to write additional information to the log file,
by editing `NLog.config` file at Rhetos Language Services server location (see the location above).
Add the following lines under `<rules>` element, before the existing rules:

```xml
<logger name="Rhetos.*" minlevel="Trace" writeTo="logfile" final="true" />
<logger name="*" minlevel="Debug" writeTo="logfile" final="true" />
```
