# Rhetos Language Service changelog

## v0.9.3-preview

* Improved signature help/active parameter for several non-common scenarios
* Keyword info implementation class is now displayed with full name (including namespace)
* More detailed logging during Rhetos app context initialization
* Full log file path displayed during initialization
* Changed C# grammar definition to allow better syntax highlight behavior when using anonymous delegates in C# snippets
* [BUGFIX] Signature help not available while typing last parameter
* [BUGFIX] Quoted strings breaking position detection for arguments
* [BUGFIX] File include directive (`<>`) not working
* [BUGFIX] Autocomplete not working at end-of-file
