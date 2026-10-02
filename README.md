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
5. [Project structure](#project-structure)
   1. [Source and test folders](#source-and-test-folders)
   2. [Root files](#root-files)
   3. [Runtime flow](#runtime-flow)
6. [How to contribute](#how-to-contribute)
   1. [Building and testing the source code](#building-and-testing-the-source-code)
   2. [Releasing a new version](#releasing-a-new-version)
7. [Troubleshooting](#troubleshooting)
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

Rhetos DSL IntelliSense is available for Visual Studio 2019 and 2022.

Before installing a new version, **uninstall the old version first:**

1. In Visual Studio under Extensions uninstall "Rhetos DSL Language Extension".
   Uninstall the extension from both VS 2019 and VS 2022, if installed.
2. In Windows "Installed apps" (or "Apps & features", depending on the Windows version) uninstall "RhetosLanguageServices".

Setup:

1. Install by downloading and running latest version of **RhetosLanguageServicesInstaller.msi** from <https://github.com/Rhetos/LanguageServices/releases>.
2. Applications with **Rhetos v4** additionally need to install [Rhetos.LanguageServicesCompatibility](https://github.com/Rhetos/LanguageServicesCompatibility) NuGet package.
  **Rhetos v5** apps, and newer, support Rhetos Language Services by default; they do not need the LanguageServicesCompatibility plugin.

## Usage

Rhetos Language Services will activate whenever `.rhe` file is opened within Visual Studio. An attempt to detect a corresponding Rhetos application is made and if successful, the system will provide IntelliSense and other features to the editor.

If your `.rhe` document resides in the directory tree inside your Rhetos application, it will automatically be detected by rule (3), so no additional configuration is needed.

For a `.rhe` document, the following rules are applied in order to find the location of corresponding Rhetos application:

1. Document is checked for explicit directive pointing to a folder which contains Rhetos application in the form of: `// <rhetosProjectRootPath="c:\some\path\to\rhetosproject" />`. It must be placed on the first non-empty line of the file (only blank lines may precede it).

2. Document folder and all parent folders are checked for `rhetos-language-services.settings.json` configuration file specifying the Rhetos application path. File format should be:

    ``` json
    {
        "RhetosProjectRootPath": "c:\\some\\path\\to\\rhetosproject"
    }
    ```

3. Document folder and all parent folders are checked for a valid Rhetos application root path. This means that folders are checked for existence of the `obj/Rhetos/DslSyntax.json` file which is produced by building the Rhetos application (a Visual Studio design-time build does not produce it).

Use rules (1) and (2) as means to override this default behavior.

## Limitations

### Changing Rhetos application after initialization

Language services server works with one Rhetos application at a time. It reads the DSL syntax from that application's `obj/Rhetos/DslSyntax.json` file; it does not load the application's `.dll` files. While any open `.rhe` document belongs to the current Rhetos application, documents of other Rhetos applications show a warning and get no code analysis. When all documents of the current application are closed, the server automatically switches to the Rhetos application used by most of the open documents.

To change the Rhetos application used in code analysis, close all `.rhe` documents of the current application, or restart Visual Studio.

### Syntax highlight keywords

Syntax highlighting has a fixed set of keywords being recognized, independent of the Rhetos application currently used.

### Documents analyzed

Language services will analyze and report errors only on **currently opened documents** in the IDE.

## Project structure

The solution `Rhetos.LanguageServices.sln` contains the LSP server, the Visual Studio extensions, the installer, and the tests.
See [docs/rhetos/README.md](docs/rhetos/README.md) for Rhetos framework background relevant to this project.

### Source and test folders

| Folder | Purpose |
|---|---|
| `src/CodeAnalysis` | Library `Rhetos.LanguageServices.CodeAnalysis` (net8.0) with the Rhetos-related logic: finds the Rhetos application for a document, loads `DslSyntax.json` and `DslDocumentation.json`, runs the Rhetos tokenizer and parser, and answers completion, hover and signature help queries. It is the only server project that references Rhetos (NuGet package `Rhetos.DslParser`). |
| `src/Server` | LSP server executable `Rhetos.LanguageServices.Server.exe` (net8.0, OmniSharp LSP library): LSP handlers, background loops for diagnostics and Rhetos application monitoring, NLog logging. |
| `src/VisualStudioExtension` | VSIX project for Visual Studio 2019. |
| `src/VisualStudioExtension17` | VSIX project for Visual Studio 2022. |
| `src/VisualStudioExtensionShared` | Source code shared by both VSIX projects: the LSP client that starts the server, the `.rhe` content type, and the refresh of Rhetos projects after a build. |
| `src/VisualStudioExtensionSharedFiles` | TextMate grammars for syntax highlighting (`Grammars/Rhetos.json` contains the fixed keyword list). |
| `src/InstallerCustomActions` | MSI custom actions: start the VSIX installation for each installed Visual Studio version (2019, 2022), and write the server location to the registry key `HKLM\SOFTWARE\Rhetos\RhetosLanguageServices`. |
| `src/RhetosLanguageServicesInstaller` | Visual Studio Installer project that builds `RhetosLanguageServicesInstaller.msi`. |
| `test/CodeAnalysis.Test` | Unit tests for `src/CodeAnalysis` (MSTest). |
| `test/LspIntegration.Test` | Integration tests with an in-memory LSP client and server. |
| `test/CommonTestTools` | Shared test helpers, for example a mock that loads `DslSyntax.json` from a folder. |
| `test/RhetosMockAppContents` | Test data: `DslSyntax.json`, `DslDocumentation.json`, and `RhetosAppFolder` folder structures for the Rhetos application detection tests. |
| `test/RhetosTestApp` | Rhetos application project that is not used by automated tests. Building it produces a real `obj/Rhetos/DslSyntax.json`. |
| `test/RhetosTestAppConcepts` | Custom DSL concept `WithParent`, referenced by `RhetosTestApp`, for testing `[ConceptParent]` support. |

### Root files

| File or folder | Purpose |
|---|---|
| `Rhetos.LanguageServices.sln` | Solution with all projects listed above. |
| `Rhetos.LanguageServices.Server.slnf` | Solution filter with the six SDK-style projects (server, `CodeAnalysis` and the test projects), for building and testing with the .NET SDK alone. |
| `Directory.Build.props` | Common MSBuild properties for all projects: version numbers, XML documentation, code analysis mode. |
| `build.bat`, `clean.bat`, `test.bat` | Command-line build (NuGet restore and MSBuild rebuild); deletion of `bin`, `obj`, `TestResults` and `dist` folders; clean, build and test run with `vstest.console.exe`. They need Visual Studio and `NuGet.exe`; for the server and its tests without Visual Studio use `tools/build-server.ps1` (see [Building and testing the source code](#building-and-testing-the-source-code)). |
| `tools/` | `build-server.ps1` (headless restore, build, test and publish of the server to `dist/server`), `ChangeVersion.ps1` (sets the version in `Directory.Build.props`, VSIX manifests and the installer project), `FindVisualStudio.bat` and `CheckLicenceInSourceFiles.bat` (used by `build.bat`), `git/gitclear.ps1` (deletes merged git branches). |
| `CHANGELOG.md`, `License.txt` | Release notes and license. |
| `docs/rhetos/` | Background documentation about the Rhetos framework, as it relates to this project. |
| `docs/lsp/` | Notes on the LSP library: `omnisharp-document-uri.md` records the measured OmniSharp `DocumentUri` behaviour (URI normalization, conversions, equality) that the server's URI handling relies on. |
| `AGENTS.md` | Instructions for coding agents and contributors, including the rules for transient implementation plans. |
| `dist/` | Folder that `clean.bat` deletes and recreates; ignored by git. |
| `temp/` | Local drafts, scratch notes and other temporary files; ignored by git. |

### Runtime flow

1. When a `.rhe` file is opened, the Visual Studio extension starts `Rhetos.LanguageServices.Server.exe` and communicates with it over LSP through standard input and output.
2. For each open document, the server finds the Rhetos application root folder by the rules described in [Usage](#usage).
3. The server loads `obj/Rhetos/DslSyntax.json` (DSL concepts and their parameters) and `obj/Rhetos/DslDocumentation.json` (concept descriptions) from that folder, and reloads them when `DslSyntax.json` changes after a build.
4. The server analyzes each open document with the Rhetos tokenizer and parser from the `Rhetos.DslParser` NuGet package.
5. The LSP handlers use the analysis results to publish diagnostics, and to provide completion, hover and signature help.

## How to contribute

Contributions are very welcome. The easiest way is to fork this repo, and then
make a pull request from your fork. The first time you make a pull request, you
may be asked to sign a Contributor Agreement.

For more info see [How to Contribute](https://github.com/Rhetos/Rhetos/wiki/How-to-Contribute) on Rhetos wiki.

### Building and testing the source code

**Server and tests, without Visual Studio**

The language server and its tests can be built and tested with the .NET SDK alone.
Solution filter `Rhetos.LanguageServices.Server.slnf` contains the six SDK-style projects:
`CodeAnalysis`, `Server`, `CommonTestTools`, `RhetosMockAppContents`, `CodeAnalysis.Test` and `LspIntegration.Test`.
It does not contain the VSIX projects and the installer, which need Visual Studio 2022.
It also leaves out `RhetosTestApp` and `RhetosTestAppConcepts`, because no test references them,
and building `RhetosTestApp` runs `rhetos build` with the Rhetos 6.0.0 CLI, which needs the .NET 8 and ASP.NET Core 8 runtimes.

* Build and test manually:
  * `dotnet build Rhetos.LanguageServices.Server.slnf`
  * `dotnet test Rhetos.LanguageServices.Server.slnf` (runs both test projects),
    or a single project: `dotnet test test/CodeAnalysis.Test`, `dotnet test test/LspIntegration.Test`
* Or run `powershell .\tools\build-server.ps1` (or `pwsh`). The script restores and builds the solution filter,
  runs both test projects, and publishes the server (framework-dependent) to `dist/server`.
  Parameters: `-Configuration` (default `Release`) and `-SkipTests`.
* The server and test projects set `RollForward` to `LatestMajor`, so they run on .NET 8 or on any newer installed .NET runtime (for example 9 or 10).

**Full solution, with Visual Studio 2022**

Open Rhetos.LanguageServices.sln in Visual Studio 2022, **build the solution** and **run the unit tests**.
This is required for the Visual Studio extensions and the installer.

Prerequisites:

* If missing, Visual Studio will automatically offer to install the required component "Visual Studio extension development".
* If Visual Studio cannot open RhetosLanguageServicesInstaller project, displaying "(incompatible)" in Solution Explorer, in Visual Studio install extension "Microsoft Visual Studio Installer Projects".

Build output:

* Build will produce installation file `RhetosLanguageServicesInstaller.msi` in `src\RhetosLanguageServicesInstaller\Debug`.
  It contains the Visual Studio extension for DSL IntelliSense and LSP server.
  See the [Installation instructions](#installation) above.

`build.bat` and `test.bat` build the full solution with MSBuild, so they need Visual Studio and `NuGet.exe`.
To build and test only the server, use `tools/build-server.ps1`.

### Releasing a new version

For example, for version `2.0.3`.

1. If building a prerelease for internal testing, run: `powershell .\tools\ChangeVersion.ps1 2.0.3 auto`
   * If building a public release run: `powershell .\tools\ChangeVersion.ps1 2.0.3`,
     and update ChangeLog.md file.
2. `clean.bat`
3. Open Rhetos.LanguageServices.sln in Visual Studio 2022
   1. Rebuild the solution.
   2. Run unit tests (the server unit tests can also be run without Visual Studio with `powershell .\tools\build-server.ps1`).
4. `powershell .\tools\ChangeVersion.ps1 2.0.3 dev`
5. The installation package is created in `src\RhetosLanguageServicesInstaller\Debug\RhetosLanguageServicesInstaller.msi`
6. If building a public release:
   * Add git tag to the current commit and push tags to GitHub.
   * Create a new release on [GitHub](https://github.com/Rhetos/LanguageServices/releases)
     for the tag, and upload the MSI file under Assets.
   * Prepare development for the next minor version with `powershell .\tools\ChangeVersion.ps1 2.1.0 dev`
     and commit with message "Development 2.1.0.".

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
