# Repository changes before VS Code support, and other major improvements

Status: plan (2026-10-02). Companion to [vs-code/README.md](vs-code/README.md). Plans live in the tracked `plans/` folder (see the root `README.md`, Project structure).

Decisions confirmed by the maintainer on 2026-10-02: the main recommendations of the VS Code plan (bundled server, monorepo, language id `rhetos-dsl`) and dropping Visual Studio 2019 support (former B2, now A0).

Revision 2026-10-02 (b): updated from docs/rhetos ([docs/rhetos/README.md](../docs/rhetos/README.md) and the six detail documents), with each change rechecked in `src/`, `test/` and the Rhetos 6.0.0 NuGet packages. Changes: seven baseline facts added (app detection, files read, Rhetos version coupling, build outputs, polling, test fixtures, Rhetos CLI runtime) and the hover row extended; A0 lists the README project-structure rows and `docs/rhetos`; A1's solution filter drops `RhetosTestApp` and `RhetosTestAppConcepts`; A3 corrects the `untitled:` crash claim and adds the non-empty `DslScript.Path` constraint; A4 states what hover can show and replaces the "Restart Visual Studio" text with the real rule; B3 decouples the test app's target framework from the server's; B5 lists what a file watcher must cover; B7 names the assets file as the app's script list and the canonical build-error format; B8's first bullet is marked done; B10 gains four rows (Trace-level load failures, comment-token end positions, RH0010 inverted range, Rhetos 6.0 fixture and test-project hygiene). Part C is unchanged, because no new prerequisite was added.

Revision (b), Bookstore: facts from github.com/Rhetos/Bookstore (`master`, `aa024a03`, 2025-09-23, Rhetos 6.0.0), checked in a clone, added the ASP.NET Core 8 runtime to A1, app identification in multi-project workspaces, the assets-file BOM, missing `ContentFiles` and package-script roots to B7, a `rhetos-5` note and a real-app smoke-test row to B10.

Part A lists changes that should land **before** the VS Code extension is implemented, ordered by priority. Part B lists major things worth fixing, changing or adding that are **not** prerequisites. Part C suggests an execution order.

Baseline facts the plan relies on (verified in the repository, in the Rhetos 6.0.0 NuGet packages and on this machine; background in [docs/rhetos](../docs/rhetos/README.md)):

| Fact | Where |
| --- | --- |
| Server and CodeAnalysis are SDK-style `net8.0` projects; VS extension projects and the vdproj installer need Visual Studio 2022 | `src/*/*.csproj`, `Rhetos.LanguageServices.sln` |
| The server only runs under .NET 8 (no `RollForward`); this machine has .NET SDK 10.0.401 and only the .NET 10 runtime | `dotnet --list-runtimes` |
| `.github/workflows/` is empty; `build.bat` is documented as not working | `README.md` "Automating the build with Build.bat has known issues" |
| URI casing problems with VS were fixed on the unmerged branch `path-case` (2022), based on `DocumentUri` | `git log master..path-case` |
| The MSI references `..\Server\obj\Debug\net8.0\apphost.exe` and the Server `PublishItems` output group | `RhetosLanguageServicesInstaller.vdproj` lines 921, 940 |
| Hover/completion texts are plain strings with `\n` and `* ` prefixes. Their documentation part is the concept's type-level `Summary` and `Remarks` from `DslDocumentation.json`; Rhetos writes no member (parameter) documentation | `ConceptQueries.CreateRhetosSignature`, `ConceptQueries.GetDocumentation`, `RhetosHoverHandler`; [dsl-syntax-json.md §4](../docs/rhetos/dsl-syntax-json.md) |
| Log file goes to `C:\ProgramData\Rhetos.LanguageServices` | `src/Server/NLog.config` |
| A folder is a Rhetos app root if `<folder>/obj/Rhetos/DslSyntax.json` exists (checked after the source directive and `rhetos-language-services.settings.json`). The server never reads `rhetos-project.assets.json` | `DslSyntaxProvider.IsValidProjectRootPath`, `RhetosProjectRootPathResolver` |
| The server reads only `obj/Rhetos/DslSyntax.json`, the optional `DslDocumentation.json` and the settings file. It loads no application DLLs (no `Assembly.Load*` in `src/**/*.cs`) and gets `.rhe` text only from LSP messages | `DslSyntaxProvider`, grep |
| The only Rhetos package in the server is `Rhetos.DslParser` 6.0.0 (netstandard2.1: tokenizer, parser, JSON readers). The only dependency on the app's Rhetos version is the DSL syntax version check: `Version` null or greater than `DslSyntax.CurrentVersion` (6.0) gives an initialization error on every document; `5.0` (Rhetos 5, and Rhetos 4 with `Rhetos.LanguageServicesCompatibility`) and `6.0` load. Every app is tokenized and parsed with the rules of `Rhetos.DslParser` 6.0.0 | `src/CodeAnalysis/CodeAnalysis.csproj`, `RhetosProjectContext.Context.LoadDslSyntax` |
| `DslSyntax.json` is written only by a full build (MSBuild target `BuildRhetosApp`, skipped when `DesignTimeBuild` is `true`) and only when its content changes. Its generator and the `DslDocumentation.json` generator run before any `.rhe` script is parsed, so both files are produced even when a script has a syntax error. `rhetos-project.assets.json` is touched on every build, design-time builds included. `rhetos build` needs no database | `Rhetos.MSBuild` 6.0.0 `build/Rhetos.MSBuild.targets`; Rhetos `DslSyntaxFile.Save`, `ResolveRhetosProjectAssets.cs`; [build-and-project-layout.md §2](../docs/rhetos/build-and-project-layout.md) |
| Change detection is polling. Every 1 s the server re-resolves the root path of documents that have none and reloads the context only when the `DslSyntax.json` timestamp changes (a change only in `DslDocumentation.json` is not reloaded). Diagnostics are published every 300 ms | `RhetosProjectMonitor`, `RhetosWorkspace.UpdateProjectContextRootPath`, `RhetosProjectContext.UpdateDslSyntax`, `PublishDiagnosticsRunner` |
| `test/RhetosMockAppContents/DslSyntax.json` and `DslDocumentation.json` are a Rhetos 5.0 build output (`Version` 5.0, type names `…, Rhetos.Dsl.DefaultConcepts, Version=5.0.0.0`). `test/RhetosTestApp` (Rhetos 6.0.0, no `Rhetos.Host`, so no database step) is not referenced by any test; its untracked local `obj/Rhetos/DslSyntax.json` has `Version` 6.0 | fixture files, `test/RhetosTestApp/RhetosTestApp.csproj`, grep of `test/` |
| `tools/rhetos.dll` in `Rhetos.MSBuild` 6.0.0 requires the .NET 8 runtime (`Microsoft.NETCore.App` and `Microsoft.AspNetCore.App` 8.0.0, no roll-forward). `RollForward` in this repository's projects does not affect it; the MSBuild property `RhetosBuildDotNetRollForward` passes `--roll-forward` to it | `~/.nuget/packages/rhetos.msbuild/6.0.0/tools/rhetos.runtimeconfig.json`, `build/Rhetos.MSBuild.targets` |

---

## Part A – Prerequisites for VS Code support

### A0. Drop Visual Studio 2019 support (decided 2026-10-02)

Why: decided by the maintainer. VS 2019 left mainstream support in April 2024. Removing it is a cheap deletion commit with no dependencies, and it reduces work in A2 (one VSIX in the installer), A4 (no VS 2019 empty-hover workaround, no client sniffing) and B1 (one VSIX to bundle the server into). It does **not** simplify A1, because A1 only covers the SDK-style projects and the VS 2019 project is not among them. Doing it first keeps every later step free of VS 2019 conditionals.

| Change | Files | Notes |
| --- | --- | --- |
| Remove the SDK 16 extension project and its solution entry | `src/VisualStudioExtension/` (whole folder), `Rhetos.LanguageServices.sln` | The 2019 project is the only user of `rhetos.lsp-server.local.json.template`; move the template next to the VS 2022 project if the override is kept |
| Remove the `VisualStudioSdk16` conditionals | `src/VisualStudioExtensionShared/LanguageClient.cs` | Keep only the VS 17 `OnServerInitializeFailedAsync` overload and `ShowNotificationOnInitializeFailed` |
| Remove the VS 2019 VSIX from the installer and from the custom action list | `RhetosLanguageServicesInstaller.vdproj` (file entry for `Rhetos.LanguageServices.VisualStudioExtension.vsix`), `InstallerCustomActions/CustomActions.cs` (`versionPrefix: "16."` entry) | If B1 (retire the MSI) is accepted, this becomes part of deleting the installer instead |
| Remove the VS 2019 empty-hover workaround comment and behavior | `RhetosHoverHandler` | Done together with A4 |
| Documentation | `README.md` (Installation says "Visual Studio 2019 and 2022"; Project structure rows for `src/VisualStudioExtension` and `src/InstallerCustomActions`), `docs/rhetos/language-services-integration.md` (§4: VS 2019 project row, VS 2019 empty-hover note), `CHANGELOG.md` (breaking change note: last version supporting VS 2019 is 6.0.0), `src/VisualStudioExtension17/Readme.md` | Rename `VisualStudioExtension17` to `VisualStudioExtension` only if the churn is acceptable; otherwise keep the name |

Verification: the solution builds in VS 2022, the VS 2022 VSIX still installs, and `grep VisualStudioSdk16` returns nothing.

### A1. Headless build and test (no Visual Studio required)

Status: implemented 2026-10-02 on branch vs-code (RollForward set per project in Server.csproj and the two test projects; script is tools/build-server.ps1; `CommonTestTools` marked `IsTestProject=false` so that `dotnet test` on the solution filter runs only the two real test projects).

Why: the VS Code extension is developed and packaged with `dotnet` + `npm`, and CI must build the server without Visual Studio. Today the only documented way to build is the full solution in VS 2022.

| Change | Files | Notes |
| --- | --- | --- |
| Add a solution filter with the SDK-style projects that the server and its tests need: `CodeAnalysis`, `Server`, `CommonTestTools`, `RhetosMockAppContents`, `CodeAnalysis.Test`, `LspIntegration.Test`. Leave out `RhetosTestAppConcepts` and `RhetosTestApp`: no test references them, and building `RhetosTestApp` runs `rhetos build` with the Rhetos 6.0.0 CLI, which needs the .NET 8 and ASP.NET Core 8 runtimes (no database: the project has no `Rhetos.Host`). Build them only to regenerate the test fixture (B10): after installing those runtimes, or with `-p:RhetosBuildDotNetRollForward=LatestMajor` (not tested whether the 6.0.0 CLI runs on .NET 10) | new `Rhetos.LanguageServices.Server.slnf` | `dotnet build Rhetos.LanguageServices.Server.slnf` and `dotnet test` on the two test projects. The tests use the Rhetos 5.0 fixture, which the server accepts, so the stale fixture does not block `dotnet test` |
| Add `<RollForward>LatestMajor</RollForward>` for the server and test projects | `src/Server/Server.csproj`, `test/*.Test/*.csproj` (or `Directory.Build.props` for all SDK projects) | Makes the server and tests run on .NET 9/10 when .NET 8 is absent. Alternative: install the .NET 8 runtime on the dev machine (needs explicit approval per the user's rules). Both can be done |
| Add a `build-server.ps1` (or extend `build.bat`) that restores, builds, tests and publishes to `dist/server` | `tools/` or repo root | Replaces the "known issues" note; `dist/` is already gitignored and `clean.bat` already recreates it |
| Document the headless workflow | `README.md` "Building and testing" | VS remains required only for the MSI and the VSIX |

Verification: `dotnet test test/CodeAnalysis.Test` and `dotnet test test/LspIntegration.Test` pass on this machine.

### A2. One server distribution artifact

Why: two clients (VS, VS Code) must ship the same server bits. Today the MSI pulls the server from `obj\Debug` paths inside the vdproj, which is not reproducible outside VS and is Debug-configuration specific.

| Change | Files | Notes |
| --- | --- | --- |
| Define the publish command and output: `dotnet publish src/Server -c Release -o dist/server` (framework-dependent). Keep the apphost `.exe` in that output for VS; the VS Code package step filters it or publishes with `UseAppHost=false` | `build-server.ps1`, `Server.csproj` (`PublishDir` property optional) | Include `NLog.config` (already copied) |
| Point the vdproj at `dist/server` content instead of project output groups, or at least at the Release configuration | `RhetosLanguageServicesInstaller.vdproj` | Can be deferred if B1 (retire MSI) is accepted soon. After A0 the installer carries a single VSIX (VS 2022) |
| Emit `server-info.json` (version, target framework, commit) into the publish output | MSBuild target in `Server.csproj` | Used by the VS Code client to pick the runtime major version and for diagnostics |

### A3. URI handling that round-trips exactly

Why: VS Code sends `file:///c%3A/...`; the server keys documents by `System.Uri` and republishes a normalized form. The `path-case` branch shows this class of bug already bit Visual Studio. `untitled:` documents do not crash the resolver (`ResolveRootPathForDocument` turns every exception into `None`, and `EnumerateParentFolders` stops on an empty folder), but `Path.GetDirectoryName(uri.LocalPath)` gives them no usable folder, so a relative source directive would be resolved against the server's working directory.

| Change | Files | Notes |
| --- | --- | --- |
| Carry `DocumentUri` (or the exact URI string) through `RhetosWorkspace`, `RhetosDocument`, `TextDocument`, `RhetosDocumentFactory`, `PublishDiagnosticsRunner`; derive `LocalPath` only for file-system access | `src/CodeAnalysis/*`, `src/Server/*` | Re-apply the `path-case` branch idea on current `master` rather than merging the stale branch (it targets Rhetos 5.0 prerelease packages). Either take the `OmniSharp.Extensions.LanguageProtocol` dependency into CodeAnalysis as that branch did, or keep CodeAnalysis protocol-free by keying on the raw string and passing `LocalPath` separately (preferred: CodeAnalysis stays reusable and testable without OmniSharp). Keep `DslScript.Path` non-empty for every scheme (today `TextDocument.DslScripts` sets it to `Uri.LocalPath`): Rhetos creates `DslSyntaxException.FilePosition` only for a non-empty path, and `CodeAnalysisRun` reads `FilePosition` without a null check |
| Guard non-file schemes: no root path, one warning diagnostic, and no resolution of a relative directive against the working directory | `RhetosProjectRootPathResolver` | Or exclude `untitled` from the client selector; do both |
| Tests: open with `file:///c%3A/`, `file:///C:/`, paths with spaces and non-ASCII; assert the published diagnostics URI string equals the opened one | `test/LspIntegration.Test` | |

### A4. Client-neutral protocol responses

Why: several responses are shaped for Visual Studio quirks and render badly or incorrectly in VS Code. Details and rationale per item are in [vs-code/protocol-compat.md](vs-code/protocol-compat.md).

| Change | Files |
| --- | --- |
| `GetTextDocumentAttributes` returns `"rhetos-dsl"`; single constant for the language id | `TextDocumentHandler` |
| Hover: build `MarkupContent` (Markdown when `hover.contentFormat` allows it, else PlainText) with the signature in a fenced block and escaped identifiers; always return `null` for "no hover" (the VS 2019 workaround goes away with A0) | `RhetosHoverHandler`, new `MarkupBuilder` in Server, `ConceptQueries` keeps producing structured data (signature, documentation) instead of pre-joined text. Content available: the signatures of all overloads of the keyword (`DslSyntax.json`) and each concept's type-level `Summary`/`Remarks` (`DslDocumentation.json`); there are no member docs, so signature-help parameters stay undocumented. Rhetos 6 flattens whitespace in summaries, but the Rhetos 5.0 fixture still contains `\n` plus indentation, so the builder normalizes whitespace |
| Completion: short `Detail`, full text in `Documentation` as `MarkupContent` | `RhetosCompletionHandler` |
| Signature help: never send `ActiveSignature`/`ActiveParameter` = 100; omit or send 0 | `RhetosSignatureHelpHandler` |
| Replace "Restart Visual Studio if you want to use a different Rhetos app." with the actual rule: the server switches to another app after all documents of the current app are closed (`RhetosWorkspace.UpdateProjectContextRootPath`, test `WillSwitchOnUnusedProjectPath`). Messages must not name an IDE | `RhetosDocument.BlockedAnalysisResult` |
| Tests for the above with VS Code style capabilities | `test/LspIntegration.Test` |

### A5. Location-neutral logging and server options

Why: `C:\ProgramData` is not writable everywhere and does not exist on Linux/macOS; the VS Code client needs a way to set the log level and to find the log file.

| Change | Files | Notes |
| --- | --- | --- |
| Default log path `${specialfolder:folder=LocalApplicationData}/Rhetos.LanguageServices/Rhetos.LanguageServices.Server.log` (or `${tempdir}`) | `src/Server/NLog.config` | Update the README troubleshooting section |
| Command-line options: `--log-file <path>`, `--log-level <level>`, `--version`, `--wait-for-debugger` | `Program.cs`, `RhetosLanguageServer.cs` | Use `System.CommandLine` or a minimal hand parser; stdout must stay JSON-RPC only, so `--version` prints and exits before the server starts |
| Keep the "Running server ... Log file ..." initialize message; additionally expose it through a custom request `rhetos/serverInfo` (path, version, log file) | `RhetosLanguageServer` | Lets the VS Code command "Open Language Server Log File" work without parsing log text |

### A6. Version propagation and release script

| Change | Files |
| --- | --- |
| `ChangeVersion.ps1` also rewrites `src/VSCodeExtension/package.json` `"version"` (no prerelease suffix) | `tools/ChangeVersion.ps1` |
| Release checklist in README gains the VSIX build, smoke test and `vsce publish` steps | `README.md` |

### A7. Continuous integration

Why: the VS Code extension needs `npm`/`vsce` steps and a server artifact; without CI every release is a manual multi-tool process on one machine. Technically Phase 1 of the VS Code plan can start without CI, so this can run in parallel, but it should exist before the first VS Code release.

| Change | Files |
| --- | --- |
| `build.yml`: setup-dotnet, build the solution filter, run both test projects, publish `dist/server`, upload artifact | `.github/workflows/build.yml` |
| Later in the VS Code plan: extension job and `release.yml` | see [vs-code/server-packaging.md](vs-code/server-packaging.md) |

---

## Part B – Major improvements that are not prerequisites

### B1. Retire the MSI: bundle the server inside the Visual Studio VSIX

The MSI exists only to put the server somewhere the VSIX can find it (registry key `HKLM\SOFTWARE\Rhetos\RhetosLanguageServices\Location`) and to launch the VSIX installers. The VSIX can carry the published server folder itself and `LanguageClient.GetLanguageServerPath` can resolve it relative to the extension assembly. Benefits: no "uninstall the old version first" ritual, no admin rights, no custom actions (`InstallerCustomActions`, `vswhere` detection, `cmd.exe` error popups), installable from the VS Marketplace and auto-updatable, no "Microsoft Visual Studio Installer Projects" extension needed to build. Keep the `rhetos.lsp-server.local.json` override for development. This also unifies packaging with VS Code (same `dist/server`). Risk: VSIX size grows by the server (a few MB); VS needs a .NET 8+ runtime, which the MSI's `.NET Core` launch condition already required.

### B2. Visual Studio 2019 support – decided, moved to A0

Decided on 2026-10-02: VS 2019 support is dropped. The work is described in prerequisite A0 and is scheduled as the first step. Nothing remains in Part B for this item.

### B3. Upgrade target framework to .NET 10 (LTS)

.NET 8 support ends in November 2026. `CodeAnalysis`, `Server`, `CommonTestTools`, `RhetosMockAppContents` and the two test projects can move to `net10.0`; their only Rhetos dependency that matters at run time is `Rhetos.DslParser` (netstandard2.1). `RhetosTestApp` and `RhetosTestAppConcepts` model a user's Rhetos app, so their target framework follows the Rhetos version they represent (Rhetos 6.0.0: `net8.0`; the unreleased 6.1.1: `net10.0`), not the server's. Combine with `RollForward` from A1 so the server still runs on whatever newer runtime users have.

### B4. OmniSharp LSP library risk

`OmniSharp.Extensions.LanguageServer` 0.19.9 is from 2023 and the project is low-activity; the repository already carries a workaround (`RhetosJsonRpcReceiver` for OmniSharp issue #609) and several log filter hacks. Options: stay and pin (acceptable short-term), or isolate the protocol layer so that a later move to another LSP stack (for example a StreamJsonRpc-based implementation) does not touch `CodeAnalysis`. A3/A4 already push in the "keep CodeAnalysis protocol-free" direction.

### B5. Event-driven diagnostics and project monitoring

`PublishDiagnosticsRunner` polls every 300 ms and `RhetosProjectMonitor` every 1 s with dedicated long-running threads. Replace with: publish after each `didChange` with a debounce (for example 200 ms) and a `FileSystemWatcher` on `obj/Rhetos/DslSyntax.json` (plus `workspace/didChangeWatchedFiles` from clients that support it). A watcher must cover what the 1 s loop does today (deletion of `DslSyntax.json`; creation of `obj/Rhetos/DslSyntax.json` in a parent folder of a document that has no root path yet, that is, the first build after the document was opened) and close one gap: a change only in `DslDocumentation.json` is not reloaded today. Do not use `rhetos-project.assets.json` as the signal: Rhetos touches it on every build, design-time builds included, while it rewrites `DslSyntax.json` only when the grammar changes. Also consider `TextDocumentSyncKind.Incremental` for large scripts. Reduces idle CPU and makes integration tests deterministic.

### B6. Robustness in handlers

* Completion and signature help throw when the document is unknown (`RhetosWorkspace.GetRhetosDocument`); return `null` like the hover handler.
* `CancellationToken` is ignored; check it before running analysis.
* `RhetosDocument._syncAnalysis` is a static lock shared by all documents; a per-document lock (or a per-project-context lock, since the Rhetos tokenizer shares `DslSyntax`) would allow parallel analysis.
* `DidChange` assumes full sync (`ContentChanges.First()`); assert or handle incremental changes explicitly.

### B7. Feature gaps worth adding (benefit both IDEs)

| Feature | Note |
| --- | --- |
| Semantic tokens for keywords | Highlighting would follow the application's actual `DslSyntax.json` instead of the fixed grammar list (README "Syntax highlight keywords" limitation) |
| Document symbols (outline) | Modules, entities, properties from the parsed concepts |
| Go to definition / find references | For `Module.Entity` and property references within open documents, later across the application's `.rhe` files. Package scripts are in the NuGet cache, where no parent folder has `obj/Rhetos`, so a package script opened from a definition gets the "No valid RhetosProjectRootPath" warning today; its app must come from the assets file |
| Workspace-wide analysis | README limitation "Documents analyzed: only currently opened documents"; needs a file enumerator per Rhetos app and incremental parsing. The authoritative script list of an app is `ContentFiles` in `obj/Rhetos/rhetos-project.assets.json`, filtered by the rules of Rhetos `DiskDslScriptLoader`: every `**/*.rhe` under the app's project folder is compiled (MSBuild item `RhetosBuild`, Rhetos 5.3+); a NuGet package's or referenced project's scripts only if they are under its `DslScripts/` folder; a project script outside the project folder (for example in a shared project, listed with an `InPackagePath` starting with `..\`) only if `rhetos-build.settings.json` declares a `Subpackage` for it. So not every `.rhe` in the workspace belongs to the app, and a `ContentFiles` entry is not always compiled. The server does not read that file today. In a multi-project workspace, identify the app by its `obj/Rhetos/` folder, not by package references: in github.com/Rhetos/Bookstore only `src/Bookstore.Service` of the 4 projects has one; its test projects get `Rhetos.MSBuild` transitively without build assets (`build/_._`), and its concept library references `Rhetos` but not `Rhetos.MSBuild`. The assets file starts with a UTF-8 BOM, and `ContentFiles` can list files that no longer exist (seen in a Bookstore clone: a deleted `.rhe` and a package script whose NuGet cache folder was removed), so the enumerator skips missing files |
| Folding ranges, formatting | Low effort for folding (brace blocks) |
| Build integration | Surface `rhetos build` errors as diagnostics; VS Code tasks / VS error list. `dotnet build` already prints them in MSBuild canonical form (`file(line,col,endline,endcol): error RHxxxx: message`, or `file(line,col)` when there is no end, from `--msbuild-format`), so a standard MSBuild problem matcher can parse them. This is also the only source of semantic errors (unresolved reference, duplicate key, concept validation): the server's parser never reports them, and in the build output they carry no file position (`Rhetos DSL: error ...`), so a matcher must tolerate a missing location |

### B8. README is partly stale

* Done in commit `2f9257f` (2026-10-02): the README no longer says that the app's `.dll` files are loaded (true only up to v1.0.0), names `obj/Rhetos/DslSyntax.json` as the detection file, and says that closing all documents of the current app switches the context (`RhetosWorkspace.UpdateProjectContextRootPath` keeps the current app while any open document uses it). The in-editor warning still says "Restart Visual Studio" (A4).
* "Automating the build with Build.bat has known issues" should be replaced by the A1 workflow.
* Add a Visual Studio Code section (installation, requirements, settings, troubleshooting) when the extension ships.

### B9. Grammar maintenance

* `Rhetos.json` `fileTypes` contains `.alpha`, apparently a leftover.
* The keyword regex is hand-maintained; generate it from CommonConcepts' `DslSyntax.json` with a small script, and keep the grammar as the single shared source for both extensions.
* `csharp.tmLanguage.plist` and `SQL.plist` are only needed by VS; VS Code has built-in `source.cs` and `source.sql`.

### B10. Small defects and hygiene

| Item | Where |
| --- | --- |
| `InstalledProductRegistration("My Asynchronous Package", "Loads asynchronously", "1.0")` is template text shown in VS "About" | `SolutionRefreshPackage.cs` |
| `Copyright (C) Omega software 2017` in assembly metadata vs 2014 in file headers; update to a current range | `Directory.Build.props`, `tools/CheckLicenceInSourceFiles.bat` |
| `AnalysisMode=AllEnabledByDefault` with broad `NoWarn`; review whether analyzer warnings are actually acted on | `Directory.Build.props` |
| `src/VisualStudioExtension/rhetos.lsp-server.local.json` exists locally (ignored); fine, but the README should mention this developer override | repo root `README.md` |
| `test/LspIntegration.Test` relies on timing (polling loops); after B5 make the tests deterministic | tests |
| `.gitignore` references `test/Rhetos.LanguageServices.Server.Test/RhetosAppFolder/**`, a path that no longer exists | `.gitignore` |
| `DslSyntax.json` load failures (unreadable JSON, missing `ConceptTypes`) are logged at Trace only, and the default NLog level is Info, so the user sees only "Waiting for Rhetos Project initialization." Log them at Warning and put the reason into that diagnostic. Part B, because both IDEs are affected equally and VS Code works without it | `RhetosWorkspace.TryInitializeWithRootPaths`, `RhetosDocument.BlockedAnalysisResult` |
| Comment tokens have no end position: `ParseCommentTokens` sets only `PositionInDslScript`, so the range check in `ApplyCommentsToResult` never matches and comment detection relies on the "comment starts on the cursor line" fallback. Set `PositionEndInDslScript` and add a test with the cursor inside a multi-line block comment | `CodeAnalysisRun.ParseCommentTokens` |
| RH0010 (invalid character in a `<file>` reference) gets an inverted range: Rhetos passes end offset 0, so `FilePosition` End is (1,1), and `CodeAnalysisRun` maps it unchanged. Clamp End to Begin at both error-mapping sites | `CodeAnalysisRun.cs` |
| The test fixture is a Rhetos 5.0 build output (see baseline facts). Add a Rhetos 6.0 `DslSyntax.json`/`DslDocumentation.json` pair generated from `test/RhetosTestApp` and keep the 5.0 pair for the Rhetos 4/5 path (a real Rhetos 5 app exists, branch `rhetos-5` of github.com/Rhetos/Bookstore with Rhetos 5.6.0 on `net5.0`, but rebuilding it needs out-of-support runtimes); tests that list keywords may need a 6.0 variant. Also: `RhetosMockAppContents.csproj` lists the untracked `FolderWithAppUnsupported\obj\Rhetos\DslSyntax.json` (the tracked file is under `MockObj`), and `CodeAnalysis.Test.csproj` references the `Rhetos` 6.0.0 package although no test file uses a Rhetos namespace directly | `test/RhetosMockAppContents`, `test/CodeAnalysis.Test` |
| Real-app smoke test with github.com/Rhetos/Bookstore (`master`, Rhetos 6.0.0, 17 `.rhe` files, 4 app-specific concepts without XML docs). Build it as its `Build.ps1` does, `dotnet build Bookstore.sln -p:RhetosDeploy=False` (the app references `Rhetos.Host`, whose `DeployRhetosApp` target otherwise runs `rhetos dbupdate` after the build), then open every script through the LSP test harness and expect no diagnostics (inferred: the build parsed the same scripts). Needs network for the NuGet restore and the .NET 8 and ASP.NET Core 8 runtimes for the Rhetos CLI, but no database; do not run Bookstore's own tests, because its `[AssemblyInitialize]` opens a database `TestScope`. Use the scripts as a Windows checkout has them: 7 of 17 start with a UTF-8 BOM, 2 contain tabs, and `core.autocrlf=true` gives CRLF. The server does not strip a leading U+FEFF; if a client sent it as text (not verified for VS or VS Code), the Rhetos tokenizer would make it a Special token and report RH0005, while the build strips the BOM when it reads the file | `test/LspIntegration.Test` or a manual/CI job; `TextDocument` |

---

## Part C – Suggested execution order

| Order | Item | Blocks |
| --- | --- | --- |
| 0 | A0 drop VS 2019 | Nothing technically, but doing it first keeps A2, A4 and B1 free of VS 2019 cases. Cheap deletion commit, no tests needed |
| 1 | A1 headless build + RollForward | Everything else; also unblocks running tests on this machine |
| 2 | A3 URI round-trip | VS Code Phase 1 acceptance (diagnostics must map to files) |
| 3 | A4 client-neutral responses | VS Code Phase 1 acceptance (hover/completion rendering) |
| 4 | A5 logging and CLI options | VS Code Phase 2 |
| 5 | A2 single distribution artifact + A6 versioning | VS Code Phase 2/3 |
| 6 | A7 CI | VS Code Phase 3 |
| 7 | B1 (retire the MSI) | Optional, but doing B1 together with A2 avoids touching the vdproj twice |
| 8 | B3, B5, B6 | Independent follow-ups |
| 9 | B7, B8, B9, B10 | Backlog |

Each Part A item is small enough for one focused commit with tests, and none of them changes the Visual Studio user experience except A5's log path (document it in `CHANGELOG.md`).
