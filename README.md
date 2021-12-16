# Rhetos Language Services

## Introduction

This project is a [Language Server Protocol](https://microsoft.github.io/language-server-protocol/) implementation for Rhetos DSL. It includes a language server and a corresponding Visual Studio extension.

## Building the project

Build the project using `build.bat`.

* If build returns error `The imported project "...\Microsoft.VsSDK.targets" was not found`, open *Rhetos.LanguageServices.sln* in Visual Studio, it will automatically offer to install the required component "Visual Studio extension development".
* If Visual Studio cannot open RhetosLanguageServicesInstaller project, displaying `(incompatible)` in Solution Explorer, in Visual Studio install extension "Microsoft Visual Studio Installer Projects".
* If build.bat fails with error `RhetosLanguageServicesInstaller.vdproj(1,1): error MSB4025: The project file could not be loaded. Data at the root level is invalid. Line 1, position 1.`, build the solution directly in Visual Studio.

Build will produce installation file in `src\RhetosLanguageServicesInstaller\Debug\RhetosLanguageServicesInstaller.msi`.
It contains the Visual Studio extension for DSL IntelliSense and LSP server.

Before running RhetosLanguageServicesInstaller.msi, uninstall any older version of RhetosLanguageServices:

1. In Windows "Apps & features" uninstall "RhetosLanguageServices".
2. In Visual Studio under Extensions uninstall "Rhetos DSL Language Extension".

## Features

1. DSL syntax highlighting
2. DSL script parse error reporting
3. Keyword autocompletion
4. Parameters autocompletion based on all tokens in the current document
5. Signature help for concept parameters
6. Keyword and signature info on hover
7. XML-style documentation from IConceptInfo implementation classes
8. Automatic refresh of Rhetos projects when Rhetos generated source files change (this refresh is needed to fix C# IntelliSense)

## Usage

Install from Visual Studio Marketplace: [Rhetos DSL Language Extension](https://marketplace.visualstudio.com/items?itemName=rhetos.rhetos-languageservices).

Rhetos Language Services will activate whenever `.rhe` file is opened within Visual Studio. An attempt to detect a corresponding Rhetos application is made and if successful, the system will provide IntelliSense and other features to the editor.

**If your `.rhe` document resides in the directory tree inside your Rhetos application, it will automatically be detected by rule (3), so no additional configuration is needed.**

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

**To change the Rhetos application used in code analysis, restart Visual Studio.**

### Syntax highlight keywords

Syntax highlighting has a fixed set of keywords being recognized, independent of the Rhetos application currently used.

### Documents analyzed

Language services will analyze and report errors only on **currently opened documents** in the IDE.

## Troubleshooting

### Rhetos application not detected after opening `.rhe` file

Make sure you have built Rhetos application at least once.

### Advanced troubleshooting

If the Rhetos Language Services server starts correctly (after opening any `.rhe` file) it will log some basic information in the Visual Studio output window under `Rhetos DSL Language Extension` source. This log will display the location from where server is running as well as log filename with detailed logging (if configured).

Use the log file for advanced troubleshooting in case of problems/errors.
