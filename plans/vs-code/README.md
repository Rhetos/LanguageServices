# VS Code support for Rhetos Language Services – implementation plan

Status: plan (2026-10-02). Nothing in this folder is implemented yet. Plans live in the tracked `plans/` folder; the root `AGENTS.md` lists the rules for plans.

Decisions confirmed by the maintainer on 2026-10-02: server bundled in the VSIX with runtime acquisition, monorepo under `src/VSCodeExtension/`, language id `rhetos-dsl`, and **Visual Studio 2019 support is dropped** (prerequisite A0).

Related plan: [../prerequisites-and-improvements.md](../prerequisites-and-improvements.md) lists the repository changes that should be done **before** this plan is executed (Phase 0 below).

Detail documents in this folder:

| Document | Content |
| --- | --- |
| [extension-structure.md](extension-structure.md) | Folder layout, `package.json` contributions, client code sketch, settings, commands, build scripts |
| [server-packaging.md](server-packaging.md) | How the .NET server is published, bundled into the VSIX, versioned and released; .NET runtime acquisition; CI |
| [protocol-compat.md](protocol-compat.md) | Server behaviors that work in Visual Studio but need verification or changes for the VS Code client |
| [testing-and-debugging.md](testing-and-debugging.md) | Local dev loop, debugging client and server, manual checklist, automated extension tests |

## 1. Premise check: can the existing server be reused?

Yes. `src/Server` is a standard Language Server Protocol server (OmniSharp.Extensions.LanguageServer 0.19.9) that speaks JSON-RPC over **stdin/stdout** and has no dependency on Visual Studio. The Visual Studio extension is only a thin client:

| Existing component | Role today | Reusable for VS Code? |
| --- | --- | --- |
| `src/CodeAnalysis` | Parsing, root path detection, DslSyntax.json loading, completion/hover/signature logic | Yes, unchanged |
| `src/Server` | LSP handlers (sync, completion, hover, signature help), diagnostics publishing, project monitor, orphan monitor | Yes, same binary; a few client-neutral fixes needed (see [protocol-compat.md](protocol-compat.md)) |
| `src/VisualStudioExtensionShared/LanguageClient.cs` | Finds server path (registry / `rhetos.lsp-server.local.json`), starts the process, connects stdio | No. VS Code equivalent is ~100 lines of TypeScript using `vscode-languageclient` |
| `src/VisualStudioExtensionShared/RheContentDefinition.cs` | Maps `.rhe` to a content type | No. Replaced by `contributes.languages` in `package.json` |
| `src/VisualStudioExtensionSharedFiles/Grammars/Rhetos.json` | TextMate grammar (scope `source.rhetos`) | Yes, as is. VS Code already ships `source.cs` and `source.sql`, so `csharp.tmLanguage.plist` and `SQL.plist` are not needed |
| `SolutionRefreshService.cs` | After a VS build, touches `project.assets.json` so C# IntelliSense picks up Rhetos generated files | No. VS-specific (DTE). No equivalent in v1, see section 7 |
| `src/RhetosLanguageServicesInstaller` (MSI) + `InstallerCustomActions` | Installs server to Program Files, writes registry key, launches VSIX installers | No. The VS Code extension bundles the server inside the VSIX |

Conclusion: one server, two clients. The server stays the single source of truth for all language features; each client only contributes a launcher, a grammar and editor metadata.

## 2. Target architecture

```
VS Code (any platform)
  +- Rhetos DSL extension (TypeScript, vscode-languageclient)
       +- contributes: language "rhetos-dsl" (.rhe), TextMate grammar, settings, commands
       +- resolves .NET runtime (ms-dotnettools.vscode-dotnet-runtime) or user-configured dotnet
       +- spawns:  dotnet <extension>/server/Rhetos.LanguageServices.Server.dll
                       |  JSON-RPC over stdio
                       v
            Rhetos.LanguageServices.Server (net8.0, framework-dependent, unchanged core)
                       +- reads <RhetosApp>/obj/Rhetos/DslSyntax.json (+ DslDocumentation.json)

Visual Studio 2022
  +- existing VSIX -> same server binary (today via MSI + registry; later possibly bundled, see prerequisites plan B1)
```

## 3. Key decisions

| Topic | Decision | Alternatives considered |
| --- | --- | --- |
| Where the extension lives | `src/VSCodeExtension/` in this repository. Shares the grammar, the version number (`Directory.Build.props` via `ChangeVersion.ps1`) and the CHANGELOG | Separate repository: cleaner npm tooling, but version/grammar drift and two release processes |
| How the server is shipped | Bundled inside the VSIX as a **framework-dependent** `dotnet publish` output (a few MB), one universal VSIX for all platforms | Self-contained per-platform VSIX (~70 MB each, 3-4 targets); download server from GitHub release on first activation (needs network, more failure modes); require user to install the MSI (defeats the purpose on non-Windows and is bad UX) |
| .NET runtime on the user machine | Depend on the `.NET Install Tool` extension (`ms-dotnettools.vscode-dotnet-runtime`) and call `dotnet.acquire` for the server's major version; fall back to `dotnet` on PATH; allow override with setting `rhetos.dotnetPath` | Only PATH lookup (fails on machines without a matching runtime; this very machine has only .NET 10 runtime while the server targets .NET 8) |
| Platform scope | Windows x64 first. Keep every choice platform-neutral (launch via `dotnet <dll>`, no `.exe` dependency, no `C:\ProgramData` log path) so Linux/macOS work without redesign. Test on Linux as a stretch goal | Windows only (would bake in `.exe` and registry assumptions again) |
| Language id | `rhetos-dsl`. This is what the server's `DocumentSelector` already expects (`TextDocumentHandler.RhetosDocumentSelector`) | `rhe` (the VS content type name); would require changing the server selector |
| Extension identity | Publisher `rhetos`, name `rhetos-dsl` -> extension id `rhetos.rhetos-dsl`, display name "Rhetos DSL" | — |
| Marketplaces | Visual Studio Marketplace (required), Open VSX (optional, for VSCodium/Cursor users) | — |
| Grammar sharing | Single source `src/VisualStudioExtensionSharedFiles/Grammars/Rhetos.json`; the extension build copies it into `syntaxes/` | Duplicate file (drift) |
| Client/server configuration | Keep the server configuration-free for v1 (same as VS). Pass only logging options via command-line arguments. Rhetos app detection stays file-system based (directive, settings file, parent folder scan) | `workspace/configuration` round-trips: more moving parts for no v1 benefit |
| LSP features in v1 | Exactly the current set: diagnostics, completion, hover, signature help, syntax highlighting | New features (semantic tokens, symbols, go-to-definition) are listed as later work in the prerequisites plan, Part B |
| Visual Studio 2019 | Dropped (prerequisites plan A0). Removes the SDK 16 VSIX project, the `VisualStudioSdk16` conditionals and the VS 2019 empty-hover workaround, so the server can return client-neutral responses without special cases | Keep as legacy build: extra conditional code in client and server for an IDE out of mainstream support since April 2024 |

## 4. Phases

### Phase 0 – Repository prerequisites

Execute Part A of [../prerequisites-and-improvements.md](../prerequisites-and-improvements.md). The minimum before Phase 1 can start is A0 (drop VS 2019), A1 (headless build), A3 (URI round-trip) and A4 (client-neutral responses). The rest can run in parallel with Phase 1 and 2.

### Phase 1 – Minimal working extension (developer build)

Deliverables:

1. `src/VSCodeExtension/` skeleton: `package.json`, `tsconfig.json`, esbuild bundling, `src/extension.ts`, `language-configuration.json`, grammar copy step, `.vscodeignore`, `README.md`, `CHANGELOG.md` link.
2. Language contribution for `.rhe` (`rhetos-dsl`) with comments, brackets and auto-closing pairs.
3. Language client that starts the server from the setting `rhetos.languageServer.path` (points to `src/Server/bin/Debug/net8.0/Rhetos.LanguageServices.Server.dll` during development) and shows server log messages in an Output channel.
4. `launch.json` for the Extension Development Host.

Acceptance criteria: open a `.rhe` file in a built Rhetos application in the Extension Development Host and see the features below. Candidate apps: `test/RhetosTestApp` after `dotnet build` (it tracks no `.rhe` file, so add one next to the `.csproj`; the build runs the Rhetos 6.0.0 CLI, which needs the .NET 8 and ASP.NET Core 8 runtimes, absent on this machine, see prerequisite A1), or a clone of github.com/Rhetos/Bookstore built with `dotnet build -p:RhetosDeploy=False` (same runtimes, no database; 17 real `.rhe` files). Expected: syntax highlighting, parse errors as squiggles that disappear after the fix, keyword completion, hover text, signature help. Open a `.rhe` file outside any Rhetos app and see the "No valid RhetosProjectRootPath" warning.

### Phase 2 – Production behavior

1. Server bundled into the VSIX from the publish output (`npm run copy-server` -> `dotnet publish`), see [server-packaging.md](server-packaging.md).
2. .NET runtime acquisition through the `.NET Install Tool` extension, with `dotnet` on PATH and `rhetos.dotnetPath` as fallbacks. Clear error dialog with a link to the README when neither works.
3. Settings: `rhetos.languageServer.path`, `rhetos.dotnetPath`, `rhetos.trace.server`, `rhetos.languageServer.logLevel`.
4. Commands: "Rhetos: Restart Language Server", "Rhetos: Open Language Server Log".
5. Status: progress notification while the runtime is being acquired; the Output channel contains the server location and the log file path (the server already logs this on initialize).
6. Multi-root and out-of-workspace files behave sensibly (the server is per-document anyway).

### Phase 3 – Packaging, release, documentation

1. `vsce package` producing `rhetos-dsl-<version>.vsix`; version taken from `Directory.Build.props` by an extended `ChangeVersion.ps1`.
2. GitHub Actions workflow: build and test server, build and package extension, upload artifacts; publish to the Marketplace on tag (secret `VSCE_PAT`).
3. Repository `README.md`: new "Visual Studio Code" installation and troubleshooting section; the release checklist extended with the VSIX and `vsce publish`.
4. Extension `README.md` (shown on the Marketplace page), icon, `LICENSE`, third-party notices.
5. `CHANGELOG.md` entry.

### Phase 4 – Automated tests

1. Extension integration tests with `@vscode/test-electron` and Mocha: activation on `.rhe`, diagnostics for a seeded file, completion and hover via `vscode.executeCompletionItemProvider` / `vscode.executeHoverProvider`. Use the mock `test/RhetosMockAppContents/DslSyntax.json` (a Rhetos 5.0 build output with DSL `Version` 5.0, which the server accepts) so no Rhetos build is needed. The server reads only `<root>/obj/Rhetos/DslSyntax.json`, and `rhetos-language-services.settings.json` names the root folder, not the file, so the test setup must copy the mock into `<fixture root>/obj/Rhetos/` first (the existing .NET tests do this with `[DeploymentItem]`, because `obj/` is gitignored).
2. `test/LspIntegration.Test`: add cases with VS Code style URIs (`file:///c%3A/...`) and VS Code client capabilities (Markdown hover, snippet completion, dynamic registration true).

### Phase 5 – Optional follow-ups (not required for the first release)

Build task integration (run `dotnet build` and surface Rhetos build errors), semantic tokens so highlighting follows the application's actual DSL keywords, document outline. These are tracked in Part B of the prerequisites plan.

## 5. Development environment

Checked on this machine on 2026-10-02:

| Tool | Needed for | Status here | Note |
| --- | --- | --- | --- |
| .NET SDK | Building the server and tests | 10.0.401 installed | Builds `net8.0` targets, but **no .NET 8 runtime is installed**, so the server and the tests cannot run until either `RollForward` is set (prerequisite A1) or a .NET 8 runtime is installed |
| Node.js LTS + npm | Extension build, `vsce`, tests | **Not installed** | Must be installed before Phase 1. Per the user's global rules, no package manager install will be run without explicit approval |
| VS Code | Running and testing the extension | 1.140.0 installed | `engines.vscode` can safely be `^1.90.0` |
| `@vscode/vsce` | Packaging and publishing | Not installed | Use as a dev dependency (`npx vsce`), no global install needed |
| Visual Studio 2022 | Only for the MSI and VSIX projects | Present (repo was built with it) | Not needed for the VS Code work once Phase 0 A1 is done |
| Marketplace publisher | `vsce publish` | Unknown | The "Rhetos" publisher used for the VS extension (CHANGELOG 0.9.8) lives in the same portal; verify that a VS Code publisher id exists and who holds the Azure DevOps PAT |

## 6. Rough effort

| Phase | Size | Comment |
| --- | --- | --- |
| 0 (prerequisites A1, A3, A4) | M | Mostly server-side changes with tests |
| 1 | S | Standard `vscode-languageclient` boilerplate |
| 2 | M | Runtime acquisition and error UX are the bulk |
| 3 | M | CI and Marketplace setup; one-time account work |
| 4 | M | Test harness setup is the main cost |

## 7. Known gaps compared to the Visual Studio extension

| VS feature | VS Code situation |
| --- | --- |
| Automatic refresh of C# IntelliSense after Rhetos generates source files (`SolutionRefreshService`) | Not available. The C# extension / C# Dev Kit in VS Code tracks `obj/` changes differently; evaluate in Phase 5 whether anything is needed at all. Document in the extension README |
| MSI installs the VS 2022 extension (VS 2019 dropped, prerequisite A0) | Not applicable |
| Grammar embeds bundled C# and SQL grammars | VS Code uses its built-in `source.cs` and `source.sql` grammars, so the result is the same or better |

## 8. Open questions for the maintainer

Resolved on 2026-10-02: monorepo layout (yes), VS 2019 support (dropped, prerequisite A0).

1. Is Linux/macOS support a goal for the first release, or only "do not prevent it"? The plan assumes the latter.
2. Does the Rhetos organization already own a Visual Studio Marketplace publisher usable for VS Code extensions, and who manages the PAT?
3. Should the VS extension move to the same "server bundled in the VSIX" model and retire the MSI (prerequisites plan B1)? That would let both clients share one packaging step.
