# Rhetos Language Service release notes

## 2.2.0 (2023-03-16)

* *Block comments* supported in syntax highlighting. They will pass syntax verification only on Rhetos v5.4+.
* Updated keywords list for syntax highlighting. Added new keywords from CommonConcepts. Removed keywords from custom extensions.
* IntelliSense: Removed surplus newlines on Remarks in concept hints.

## 2.1.0 (2022-05-23)

* Rhetos DSL IntelliSense now supports **Visual Studio 2022**,
  along with the existing Visual Studio 2019.
* Updated dependencies for VS2019 to fix build issues.
  The extension for Visual Studio 2019 now requires **v16.10 or newer**.

## 2.0.3 (2022-03-25)

* Updated for compatibility with Rhetos 5.0.0.
* New MSI installer that contains two Rhetos DSL IntelliSense components:
  * Visual Studio extension
  * Rhetos Language Services server
* Improved error handling, initialization and logging.
* Refreshing C# IntelliSense for newly generated files to work with latest update of VS2019 (updating timestamp on project.assets.json).
* Fixed rhetos project initialization attempts when no documents are opened.
* Added mechanism to self-terminate if host process exits without requesting shutdown.
* Workaround for OmniSharp deserialization of Initialize JsonRpc request and VS2019 Hover handling.

## 1.0.0 (2020-05-14)

* Updated for compatibility with Rhetos release 4.0.0.

## 0.9.8-preview

* Updated VS marketplace publisher

## 0.9.7-preview

* Refreshing projects in solution after build will no longer impact VS UI

## 0.9.6-preview

* Changed XML documentation display format
* Minor wording changes in LSP warning
* Extension will now monitor solution projects for changes in Rhetos generated files and will refresh projects accordingly to fix IntelliSense
* [BUGFIX] Hover while Visual Studio is loading will no longer generate LSP exception message
* Upgraded to latest Rhetos 4.0 build

## 0.9.5-preview

* Language Server will now use Rhetos project DLLs instead of built application DLLs
* Added asynchronous checking for Rhetos project configuration changes and appropriate warnings
* Completion at parameter positions will now offer all non-keyword tokens found in current document
* Completion will now correctly handle concepts with `ConceptParentAttribute`
* Configuration keys for overriding Rhetos project location have been changed to `rhetosProjectRootPath` (from `rhetosAppRootPath`)
* [BUGFIX] XML documentation will now correctly be displayed for concepts in plugins

## 0.9.3-preview

* Improved signature help/active parameter for several non-common scenarios
* Keyword info implementation class is now displayed with full name (including namespace)
* More detailed logging during Rhetos app context initialization
* Full log file path displayed during initialization
* Changed C# grammar definition to allow better syntax highlight behavior when using anonymous delegates in C# snippets
* [BUGFIX] Signature help not available while typing last parameter
* [BUGFIX] Quoted strings breaking position detection for arguments
* [BUGFIX] File include directive (`<>`) not working
* [BUGFIX] Autocomplete not working at end-of-file
