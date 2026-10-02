# Testing and debugging the VS Code extension

Back to [README.md](README.md).

## 1. Local development loop

| Step | How |
| --- | --- |
| Build the server | `dotnet build src/Server/Server.csproj` (output `src/Server/bin/Debug/net8.0/Rhetos.LanguageServices.Server.dll`). Needs a runtime able to run it: .NET 8, or any newer major once `RollForward=LatestMajor` is set (prerequisite A1). This machine has only .NET 10 runtime |
| Build the extension | `cd src/VSCodeExtension && npm install && npm run watch` |
| Run | F5 with the "Run Extension" launch configuration (Extension Development Host). In that window set `"rhetos.languageServer.path": "<repo>/src/Server/bin/Debug/net8.0/Rhetos.LanguageServices.Server.dll"` in user settings, so the bundled `server/` folder is not required |
| Test data | `test/RhetosTestApp` is a real Rhetos 6 application project. `dotnet build test/RhetosTestApp` produces `obj/Rhetos/DslSyntax.json` and `DslDocumentation.json` (no database needed for the build, but the Rhetos 6.0.0 CLI needs the .NET 8 and ASP.NET Core 8 runtimes, see prerequisite A1). Add a `.rhe` file next to it for manual testing. `test/RhetosMockAppContents/RhetosAppFolder/*` contains ready-made folders with `rhetos-language-services.settings.json` and mock syntax files used by the existing unit tests |
| Watch the protocol | Set `"rhetos.trace.server": "verbose"`; the Output channel "Rhetos DSL Language Server" shows every JSON-RPC message plus the server's `window/logMessage` lines |
| Server log file | Path is printed on initialize ("Log file: '...'"). Raise the level with `--log-level Trace` (prerequisite A5) or by editing `NLog.config` as the README describes |

`.vscode/launch.json`:

```jsonc
{
  "configurations": [
    {
      "name": "Run Extension",
      "type": "extensionHost",
      "request": "launch",
      "args": ["--extensionDevelopmentPath=${workspaceFolder}/src/VSCodeExtension"],
      "outFiles": ["${workspaceFolder}/src/VSCodeExtension/dist/**/*.js"],
      "preLaunchTask": "npm: compile - src/VSCodeExtension"
    },
    {
      "name": "Extension Tests",
      "type": "extensionHost",
      "request": "launch",
      "args": [
        "--extensionDevelopmentPath=${workspaceFolder}/src/VSCodeExtension",
        "--extensionTestsPath=${workspaceFolder}/src/VSCodeExtension/out/test/suite/index"
      ],
      "outFiles": ["${workspaceFolder}/src/VSCodeExtension/out/**/*.js"]
    }
  ]
}
```

## 2. Debugging the server while VS Code drives it

Options, from simplest to most convenient:

1. **Attach**: start the Extension Development Host, open a `.rhe`, then in Visual Studio or in VS Code (C# extension) use "Attach to Process" on `dotnet Rhetos.LanguageServices.Server.dll`. Fine for anything after initialization.
2. **Wait for debugger**: add a `--wait-for-debugger` switch (or env var `RHETOS_LSP_WAIT_FOR_DEBUGGER=1`) to the server that spins until `Debugger.IsAttached` for up to 30 s. The VS Code client passes it through when `rhetos.languageServer.debug` is set (hidden setting). Needed to debug `initialize` and root-path detection.
3. **In-process tests**: `test/LspIntegration.Test` hosts client and server in one process with OmniSharp's testing package. This is the fastest way to reproduce a protocol issue once it is understood; add a test with the exact VS Code request captured from the trace output.

## 3. Manual test checklist (per release)

| # | Scenario | Expected |
| --- | --- | --- |
| 1 | Install VSIX in a clean profile (`code --profile-temp --install-extension ...`) on a machine without .NET 8 runtime | `.NET Install Tool` acquires the runtime with a progress notification; the server starts |
| 2 | Open `.rhe` in a built Rhetos app | Highlighting, diagnostics, completion, hover, signature help all work |
| 3 | Introduce a syntax error, then fix it | Squiggle appears within ~1 s, disappears after the fix |
| 4 | `.rhe` outside any Rhetos app | One warning diagnostic explaining the root path options |
| 5 | Directive `// <rhetosProjectRootPath="..." />` on line 1, relative and absolute | Context switches to that app |
| 6 | `rhetos-language-services.settings.json` in a parent folder, valid and invalid | Valid: used; invalid: warning with the configuration file path |
| 7 | App not built yet, then `dotnet build` | "must be built at least once" warning, then normal diagnostics without reopening the file |
| 8 | Two apps open at once | Second app's file shows the "different Rhetos app" warning |
| 9 | Close all `.rhe` files | Diagnostics cleared in Problems view |
| 10 | "Rhetos: Restart Language Server" | New process id in the Output channel, features keep working |
| 11 | Kill the server process in Task Manager | Client restarts it; after 5 crashes in a short time it stops with a message |
| 12 | "Developer: Reload Window", close VS Code | No orphan `dotnet` processes |
| 13 | Large file (e.g. 5000 lines) | Typing stays responsive; completion under ~300 ms |
| 14 | Path with spaces and non-ASCII characters | Diagnostics map to the correct file (URI round-trip) |
| 15 | `rhetos.languageServer.path` pointing to a non-existing file | Clear error message, no silent failure |
| 16 | Hover rendering | Signature in a code block, documentation paragraphs intact, no stray bullets or italics |

## 4. Automated extension tests

Tooling: `@vscode/test-electron` downloads a VS Code build and runs Mocha inside it. On Linux CI it needs `xvfb-run`.

Fixture: a workspace folder under `src/VSCodeExtension/test/fixtures/` containing `rhetos-language-services.settings.json` whose `RhetosProjectRootPath` points at a folder into which the test setup copies the committed mock `test/RhetosMockAppContents/DslSyntax.json` as `obj/Rhetos/DslSyntax.json` (the server reads only that path, and `obj/` is gitignored), plus a couple of `.rhe` files with known errors. This avoids building a Rhetos app in CI. The server path for tests is set through the `rhetos.languageServer.path` setting in the fixture's `.vscode/settings.json`, pointing to the publish output.

Tests:

| Test | Assertion |
| --- | --- |
| Activation | Opening a `.rhe` activates `rhetos.rhetos-dsl`; `languageId` is `rhetos-dsl` |
| Diagnostics | `vscode.languages.getDiagnostics(uri)` contains the expected error message and range within 5 s (poll) |
| Completion | `vscode.executeCompletionItemProvider` at a known position returns `Entity`, `Module` and other keywords from the mock syntax; items have `documentation` |
| Hover | `vscode.executeHoverProvider` on a keyword returns a `MarkdownString` whose value contains the signature |
| Signature help | `vscode.executeSignatureHelpProvider` after `Entity ` returns at least one signature with `activeParameter` 0 |
| Restart command | `rhetos.restartLanguageServer` resolves and diagnostics are republished |
| URI round-trip | Diagnostics for a file in a folder with spaces and `č` in the name are attached to the opened document |

## 5. Server-side test additions (`test/LspIntegration.Test`)

* A test fixture that initializes with VS Code's actual client capabilities (capture once from a trace and store as JSON): `hover.contentFormat = [markdown, plaintext]`, `completion.completionItem.documentationFormat`, `dynamicRegistration = true` everywhere, `positionEncodings = [utf-16]`.
* Documents opened with `file:///c%3A/...` style URIs; assert the `PublishDiagnosticsParams.Uri` string equals the opened one.
* Hover returns `MarkupContent` with kind Markdown when the client supports it and PlainText otherwise.
* Signature help never returns `ActiveSignature`/`ActiveParameter` out of range.

These run on `dotnet test` without VS Code and give fast feedback for the protocol items in [protocol-compat.md](protocol-compat.md).
