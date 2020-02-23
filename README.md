# Rhetos Language Services

## Introduction

This project is a [Language Server Protocol](https://microsoft.github.io/language-server-protocol/) implementation for Rhetos DSL. It includes a language server and a corresponding Visual Studio extension.

## Building the project

Build the project using `build.bat`. This will produce `.vsix` file in the `./dist` folder. It is a Visual Studio extension containing both the extension code and LSP server binaries needed to run it.

Install the `.vsix` as you would install any other Visual Studio extension.

## Features

1. DSL syntax highlighting 
2. DSL script parse error reporting
3. Keyword autocompletion
4. Signature help for concept parameters
5. Documentation and signature info on hover

## Usage

Rhetos Language Services will activate whenever `.rhe` file is opened within Visual Studio. An attempt to detect a corresponding Rhetos application is made and if successful, the system will provide intellisense and other features to the editor.

**If your `.rhe` document resides in the directory tree inside your Rhetos application, it will automatically be detected by rule (3), so no additional configuration is needed.**

For a `.rhe` document, the following rules are applied in order to find the location of corresponding Rhetos application:

1. Document is checked for explicit directive pointing to a folder which contains Rhetos application in the form of: `// <rhetosAppRootPath="c:\some\path\to\rhetosapp" />`. It must be placed on the first line of the file.

2. Document folder and all parent folders are checked for `rhetos-language-services.settings.json` configuration file specifying the Rhetos application path. File format should be:

    ``` json
    {
        "RhetosAppRootPath": "c:\\some\\path\\to\\rhetosapp"
    }
    ```

3. Document folder and all parent folders are checked for a valid Rhetos application root path. This generally means that folders are checked for existance of the `RhetosAppEnvironment.json` file which is produced by building the Rhetos application.

Use rules (1) and (2) as means to override this default behavior.

## Limitations

### Changing Rhetos application after initialization

Initializating language services server with specific Rhetos application causes `.dll` files of that application to be loaded (for DSL concept discovery). As a consequence, once initialized with a specific Rhetos application, services will only work for that application for the duration of the process runtime.

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
