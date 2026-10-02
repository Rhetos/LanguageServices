# Rhetos Language Services: integration with the Rhetos framework

This repository is an LSP server for Rhetos DSL (`.rhe`) scripts, plus a Visual Studio client. The server does not load or execute any assembly of the Rhetos application. It reads two JSON files that the Rhetos build writes to `<app>/obj/Rhetos/`: `DslSyntax.json` (all concept types and their members) and the optional `DslDocumentation.json` (XML doc summaries). It deserializes them with Rhetos' own `DslSyntaxFile` and `DslDocumentationFile` classes. It analyzes each open document with Rhetos' own `Tokenizer` and `DslParser` from the `Rhetos.DslParser` 6.0.0 NuGet package, and gets cursor context from the parser's `OnKeyword`, `OnMemberRead` and `OnUpdateContext` events. All Rhetos code lives in `src/CodeAnalysis` (net8.0). `src/Server` adds the OmniSharp LSP layer (0.19.9). One process serves one Rhetos application at a time. Up to v1.0.0 the server loaded the application's DLLs. Rhetos v5+ apps produce the JSON files themselves, and Rhetos v4 apps need the external `Rhetos.LanguageServicesCompatibility` package to produce them.

Legend: **V** = verified in code, git history or a file (path cited). **I** = inference, not directly verified. Paths without a prefix are relative to this repo root. `Rhetos6:` means the Rhetos framework repository root (github.com/Rhetos/Rhetos, see README Sources). The Rhetos source files in `Rhetos6:src/Rhetos.Core.DslParser` and `Rhetos6:src/Rhetos.Core.Integration` are identical between tag `v6.0.0` and the current HEAD of Rhetos6. `git diff --stat v6.0.0 HEAD` shows only csproj and nuspec changes there (V).

## 1. Component map

### 1.1 Main flow (V)

| Step | What happens | Code |
|---|---|---|
| 1 | VS starts `Rhetos.LanguageServices.Server.exe` as a child process and talks LSP over stdin/stdout | `src/VisualStudioExtensionShared/LanguageClient.cs`, `src/Server/RhetosLanguageServer.cs` (`Run`) |
| 2 | `textDocument/didOpen`/`didChange` stores the text. A new `RhetosDocument` immediately resolves its Rhetos app root path | `Handlers/TextDocumentHandler.cs` → `RhetosWorkspace.UpdateDocumentText` → `RhetosDocument.UpdateText` → `RhetosProjectRootPathResolver.ResolveRootPathForDocument` |
| 3 | Every 1 s: pick the root path used by most open documents. Load `DslSyntax.json` + `DslDocumentation.json` into the single `RhetosProjectContext`. Reload when the `DslSyntax.json` timestamp changes | `Services/RhetosProjectMonitor.cs` → `RhetosWorkspace.UpdateRhetosProjectContext` → `RhetosProjectContext.Initialize/UpdateDslSyntax` → `DslSyntaxProvider.Load` |
| 4 | Every 300 ms: for changed documents run a full analysis (Rhetos `Tokenizer` → `TokenizerExplicitTokens` → Rhetos `DslParser`) and publish diagnostics | `Services/PublishDiagnosticsRunner.cs` → `RhetosDocument.GetAnalysis()` → `CodeAnalysisRun.RunForPosition(null)` |
| 5 | Completion, hover and signature help run a position-scoped analysis: the text is truncated at the end of the cursor's line, and the result is cached per offset. The handlers then query `ConceptQueries` | `RhetosDocument.Get*AtPosition`, `TextDocument.GetTruncatedAtNextEndOfLine`, `Services/ConceptQueries.cs` |

### 1.2 Classes in `src/CodeAnalysis` (assembly `Rhetos.LanguageServices.CodeAnalysis`)

| Folder | Class | Responsibility |
|---|---|---|
| `Parsing/` | `CodeAnalysisError` | Diagnostic: begin/end `LineChr`, Rhetos error code, message, severity `Error`/`Warning` |
| `Parsing/` | `CodeAnalysisResult` | Output of one run: Rhetos tokens, comment tokens, concept context stack, keyword token at cursor, next keyword, valid concept types + active parameter, tokenizer and parser errors |
| `Parsing/` | `CodeAnalysisRun` | One analysis run: creates Rhetos `Tokenizer` and `DslParser`, subscribes to parser events, converts `DslSyntaxException` to `CodeAnalysisError`, re-scans comments |
| `Parsing/` | `ConceptTypeTools` | Signature text (`Keyword <Key: Type>.<Key: Type> <Param: Type>`) and parameter list from `ConceptType.Members` where `IsParsable` |
| `Parsing/` | `DocumentKey` | Identity of an open document: the URI string exactly as the caller supplies it (the server passes the canonical LSP form), and `LocalPath`, a fully qualified path for `file` URIs and null for other schemes. Design decision: CodeAnalysis references no LSP library (no OmniSharp), so it can be used and tested without one; the server converts LSP URIs with `DocumentKeyExtensions` (section 1.3). URI details: [omnisharp-document-uri.md](../lsp/omnisharp-document-uri.md) |
| `Parsing/` | `LineChr` | Zero-based line/character struct |
| `Parsing/` | `RhetosDocument` | One open document: text, `RootPathConfiguration`, analysis cache, completion/hover/signature queries, "blocked analysis" warnings |
| `Parsing/` | `RhetosSignature` | DTO: `ConceptType`, parameters, signature text, documentation |
| `Parsing/` | `SimpleExternalResourceReader` | Rhetos `IExternalTextReader` stub. It returns `"<name>"` instead of reading referenced files or SQL resources |
| `Parsing/` | `TextDocument` | Text + `DocumentKey`. Implements Rhetos `IDslScriptsProvider` (one `DslScript`, `Path = LocalPath ?? Uri`, never empty; `LocalPath` is null for non-file schemes). Offset ↔ line/char, truncation |
| `Parsing/` | `TokenizerExplicitTokens` | Rhetos `ITokenizer` that returns a precomputed token list, used to feed `DslParser` |
| `Services/` | `ConceptQueries` | Keyword → signatures + docs. Valid child concepts for a parent `ConceptType`. Doc lookup by `AssemblyQualifiedName` |
| `Services/` | `IRhetosProjectRootPathResolver`, `RhetosProjectRootPathResolver` | Find the Rhetos app root for a document (section 2.1) |
| `Services/` | `RhetosDocumentFactory` | Creates `RhetosDocument` with the injected services |
| `Services/` | `RhetosProjectContext` | The single loaded app: `DslSyntax`, `DslDocumentation`, case-insensitive keyword → `ConceptType[]` map, `DslSyntax.json` timestamp, version error |
| `Services/` | `RhetosWorkspace` | Open documents and change times. Chooses the context root path and invalidates caches on context change |
| `Tools/` | `AppLanguageServicesOptions` | Schema of `rhetos-language-services.settings.json` (`RhetosProjectRootPath`) |
| `Tools/` | `IDslSyntaxProvider`, `DslSyntaxProvider` | Load the JSON files from `<root>/obj/Rhetos` through Rhetos file classes. Validity check, timestamp |
| `Tools/` | `RhetosNetCoreLogProvider`, `RhetosNetCoreLogger` | Adapter from Rhetos `ILogProvider`/`ILogger` to `Microsoft.Extensions.Logging` |
| `Tools/` | `RootPathConfiguration`, `RootPathConfigurationType` | Resolved root path + source (`None`, `DetectedRhetosApp`, `ConfigurationFile`, `SourceDirective`) + context text |

### 1.3 Classes in `src/Server` (exe `Rhetos.LanguageServices.Server`)

| Folder | Class | Responsibility |
|---|---|---|
| root | `Program` | Entry point. Runs the server and logs fatal errors to NLog |
| root | `RhetosLanguageServer` | Builds the OmniSharp server on stdio and registers handlers + DI singletons. Configures log filters. Disables dynamic registration (VS 2022 workaround, CHANGELOG 6.0.0 #15). Starts background services on `initialized`. Refuses a second instance per process |
| `Handlers/` | `TextDocumentHandler` | didOpen/didChange/didClose/didSave with full sync. Document selector: language `rhetos-dsl` |
| `Handlers/` | `RhetosCompletionHandler`, `RhetosHoverHandler`, `RhetosSignatureHelpHandler` | LSP features (section 4) |
| `Services/` | `PublishDiagnosticsRunner` | 300 ms loop. Publishes diagnostics for changed documents and empty lists for closed ones |
| `Services/` | `RhetosProjectMonitor` | 1 s loop that calls `RhetosWorkspace.UpdateRhetosProjectContext` |
| `Services/` | `OrphanedProcessMonitor` | 3 s loop. Calls `Environment.Exit(2)` when the host `processId` from `initialize` has exited |
| `Tools/` | `DocumentKeyExtensions` | OmniSharp `DocumentUri` → `DocumentKey`: the URI string is `DocumentUri.ToString()`, and `LocalPath` is set only for the `file` scheme ([omnisharp-document-uri.md §4](../lsp/omnisharp-document-uri.md#4-what-the-server-does-with-this-since-version-610)) |
| `Tools/` | `PositionExtensions` | LSP `Position` ↔ `LineChr` |
| `Tools/` | `RhetosJsonRpcReceiver` | Overrides OmniSharp `LspServerReceiver.GetRenor` (workaround for OmniSharp issue #609, comment in `RhetosLanguageServer.cs`) |

Logging: `src/Server/NLog.config` writes `Info`+ to `%ProgramData%/Rhetos.LanguageServices/Rhetos.LanguageServices.Server.log` (2 MB, 1 archive). OmniSharp forwards `Rhetos.LanguageServices.*` messages at `Information` level and above to the LSP client log (`ConfigureLoggingDefaults`) (V).

## 2. Rhetos app detection and loading

### 2.1 Root path resolution per document (V, `src/CodeAnalysis/Services/RhetosProjectRootPathResolver.cs`)

| Order | Rule | Exact check | Relative path base |
|---|---|---|---|
| 1 | Source directive | `Regex.Match(text, @"^\s*//\s*<rhetosProjectRootPath=""(.+)""\s*/>")` without `Multiline`. The directive must be the first non-whitespace text of the file. Blank lines before it are allowed (fixture `EmptyFolder/directive.rhe`) | Document folder |
| 2 | Settings file | First `rhetos-language-services.settings.json` in the document folder or any parent, read with Newtonsoft. A missing `RhetosProjectRootPath` key throws, and the result becomes `None` with the exception message, so rule 3 is **not** tried (test `FaultyConfiguration`) | Settings file folder |
| 3 | Detection | First folder, walking up from the document folder, where `<folder>/obj/Rhetos/DslSyntax.json` exists (`DslSyntaxProvider.IsValidProjectRootPath`) | n/a |

* Rules 1 and 2 do not check that the target exists. `RhetosWorkspace.TryInitializeWithRootPaths` later skips invalid paths and logs at Trace: "is not a valid Rhetos Project path" (V).
* `RhetosDocument.UpdateRootPathConfiguration` runs a full re-resolve only when the document has no root path yet or its directive was deleted. Otherwise only directive edits apply. The monitor calls this every cycle, so a document opened before the first build picks up the app about 1 s after `DslSyntax.json` appears (test `WillInitializeOnAssetsFolderNewlyCreated`) (V).
* **README (corrected 2026-10-02):** rule (3) formerly said detection checks `obj/Rhetos/rhetos-project.assets.json`; the code checks `obj/Rhetos/DslSyntax.json` and never reads the assets file (V, grep of `src/`).

### 2.2 Files read from the Rhetos app (V)

| File | Reader | Required | Failure behavior |
|---|---|---|---|
| `<root>/obj/Rhetos/DslSyntax.json` | Rhetos `DslSyntaxFile(new RhetosBuildEnvironment { CacheFolder = <root>/obj/Rhetos }, logProvider).Load()` in `Tools/DslSyntaxProvider.cs` | Yes | Read/JSON exceptions, and `ConceptTypes == null` (`ExtractKeywords` throws), are caught in `RhetosWorkspace.TryInitializeWithRootPaths` and logged **at Trace only**. The context stays uninitialized, so the document shows "Waiting for Rhetos Project initialization." The load is retried every 1 s |
| `<root>/obj/Rhetos/DslDocumentation.json` | Rhetos `DslDocumentationFile(...).Load()` | No | Any exception → `null`. Hover then shows "*No XML documentation information was found for this Rhetos App*" (`ConceptQueries.GetDocumentation`) |
| `rhetos-language-services.settings.json` | `JsonConvert.DeserializeObject<AppLanguageServicesOptions>` | No | See rule 2 |
| `.rhe` text | From LSP messages, never from disk | n/a | n/a |
| **Not read** | `rhetos-project.assets.json`, any `.dll`, `rhetos-build.settings.json`, files/SQL resources referenced from DSL | n/a | No `Assembly.Load*`/`AssemblyLoadContext` in `src/` |

Reload: `RhetosProjectContext.UpdateDslSyntax` compares `FileInfo.LastWriteTime` of `DslSyntax.json` with the value stored at load time. If the file has disappeared, it drops the context (`current = null`). After any context change, `RhetosWorkspace` clears every document's analysis cache and marks the document as changed, so diagnostics are republished (V).

DSL version check in `RhetosProjectContext.Context.LoadDslSyntax` (V). This implements the contract described in `Rhetos6:src/Rhetos.Core.DslParser/DslSyntax.cs` remarks:

| `DslSyntax.Version` in file | Result |
|---|---|
| `null` | Error "Cannot detect the application's DSL syntax version (Rhetos x)..." |
| `> DslSyntax.CurrentVersion` (`6.0` in Rhetos.DslParser 6.0.0) | Error "Please install the latest version of Rhetos Language Services (IntelliSense)..." |
| `<= 6.0` | Loaded |

On a version error the context counts as initialized but carries `InitializationError`. Every document gets that error as a diagnostic, analysis is blocked (completion returns 0 items), and the load is not retried until the root path or the timestamp changes. Rhetos' `DslSyntaxFile.Load` also logs its own warning (test `ProjectContextTests.UnsupportedDslSyntaxVersion`) (V).

### 2.3 "Only one Rhetos app per process"

| Fact | Source |
|---|---|
| `RhetosProjectContext` holds one `Context` and is a DI singleton. `RhetosLanguageServer.RunAndWaitExit` throws if a second server instance is created in the process | `Services/RhetosProjectContext.cs`, `src/Server/RhetosLanguageServer.cs` (V) |
| Root paths of open documents are ordered by document count. The current path is kept while any open document uses it. Otherwise the server initializes the most used valid path | `RhetosWorkspace.UpdateProjectContextRootPath`, `GetRootPathsFromDocuments` (V) |
| Documents with another root get a warning ("...initialized with Rhetos app at '...'. ... Restart Visual Studio if you want to use a different Rhetos app.") and no analysis | `RhetosDocument.BlockedAnalysisResult` (V) |
| The context switches automatically after all documents of the first app are closed | test `ProjectContextTests.WillSwitchOnUnusedProjectPath` (V) |
| The README (until 2026-10-02) gave the reason as "`.dll` files of that application to be loaded". That was true up to v1.0.0: `Assembly.LoadFrom` + `AssemblyResolve`, the Rhetos `PluginScanner` searching for `IConceptInfo`, and a shadow-copy `AppDomain` | tag `v1.0.0`: `src/Rhetos.LanguageServices.Server/Services/RhetosAppContext.cs`, `Program.cs` (V) |
| The current code loads only JSON, so the limit now comes from the single-context design, not from assemblies that cannot be unloaded. The "Restart Visual Studio" advice is stricter than the current behavior requires | I |

### 2.4 Rhetos-version-specific behavior

| Rhetos | How the JSON files are produced | Branch in this repo | Source |
|---|---|---|---|
| v4 | The app must reference NuGet `Rhetos.LanguageServicesCompatibility`. Its generators write `obj\Rhetos\DslSyntax.json` + `DslDocumentation.json` with DSL `Version = 5.0` | None. Same code path as v5+ | `README.md` Installation. LanguageServicesCompatibility repo (github.com/Rhetos/LanguageServicesCompatibility): `Readme.md`, `src\Rhetos.LanguageServicesCompatibility\Rhetos.Dsl.Parser\DslSyntax.cs` (V) |
| v5+ | Rhetos build generators `DslSyntaxFileGenerator`/`DslDocumentationFileGenerator` (`IGenerator`). `CacheFolder` = `$(BaseIntermediateOutputPath)Rhetos` | None | `Rhetos6:src/Rhetos.Core/Dsl/DslSyntaxFileGenerator.cs`, `Rhetos6:src/RhetosVSIntegration/MsBuildIntegrationBuild/Rhetos.MSBuild.targets` (V) |
| v5.4+ | Block comments pass syntax verification | None (handled by the Rhetos tokenizer) | `CHANGELOG.md` 2.2.0 (V) |
| v6 | External references may point to SQL resources | None. Never resolved (`SimpleExternalResourceReader`) | `CHANGELOG.md` 6.0.0 (V) |

The only version logic in `src/` is the `DslSyntax.Version` comparison in section 2.2 (V).

### 2.5 Integration history

| Release | Rhetos target | Integration | Source |
|---|---|---|---|
| 0.9.5-preview | Rhetos 4.0 builds | "use Rhetos project DLLs instead of built application DLLs". Commit `cd22f03` added shadow assembly copying to avoid locking DLLs | `CHANGELOG.md`, `git log` (V) |
| 1.0.0 (2020-05-14) | Rhetos 4.0.0 | .NET Framework 4.7.2. Package `Rhetos` 4.0.0. Reads `rhetos-project.assets.json` (`RhetosProjectContentProvider`), loads the listed assemblies, scans plugins. OmniSharp 0.14.2 | tag `v1.0.0` csproj and `RhetosAppContext.cs` (V) |
| 2.0.3 (2022-03-25) | Rhetos 5.0.0 (v4 through the compatibility package) | Redesign: net5.0, `Rhetos.DslParser` 5.0.0, `DslSyntax.json` provider, timestamp monitoring (commits `f76833b`, `ee9d016`, `a658894`). OmniSharp 0.19.2 | `CHANGELOG.md`, tag `v2.0.3` csproj (V) |
| 2.1.0 / 2.2.0 / 2.3.0 | Rhetos 5.x | VS 2022 support. Block-comment highlighting. `Rhetos.DslParser` 5.4.0 at tag `v2.3.0` | `CHANGELOG.md`, tag csproj (V) |
| 6.0.0 (2025-09-08) | Rhetos 6 | .NET 8, `Rhetos.DslParser` 6.0.0, OmniSharp 0.19.9, VS 2022 dynamic-registration fix | `CHANGELOG.md`, `src/CodeAnalysis/CodeAnalysis.csproj` (V) |
| 6.1.0-dev (current) | Rhetos 6 | No `src/` or `test/` code changes since `v6.0.0` except version numbers | `Directory.Build.props`, `git diff --stat v6.0.0 HEAD` (V) |

The jump from 2.3.0 to 6.0.0 aligns this package's version with the Rhetos major version (I).

## 3. Rhetos NuGet dependencies

### 3.1 Package references (V, from `.csproj` files)

| Project | Package | Version | Note |
|---|---|---|---|
| `src/CodeAnalysis` | `Rhetos.DslParser` | 6.0.0 | The only Rhetos package in the shipped server |
| `src/CodeAnalysis` | `Microsoft.Extensions.Logging`, `.Console` | 8.0.1 | |
| `src/Server` | `OmniSharp.Extensions.LanguageServer` | 0.19.9 | LSP library (MediatR, Newtonsoft come transitively) |
| `src/Server` | `NLog`, `NLog.Extensions.Logging` | 6.0.3 | |
| `test/CodeAnalysis.Test` | `Rhetos` | 6.0.0 | No `using` of a Rhetos namespace in its `.cs` files. Possibly unused (I) |
| `test/LspIntegration.Test` | `OmniSharp.Extensions.LanguageProtocol.Testing` | 0.19.9 | |
| `test/RhetosTestAppConcepts` | `Rhetos`, `Rhetos.CommonConcepts` | 6.0.0 | |
| `test/RhetosTestApp` | `Rhetos.CommonConcepts`, `Rhetos.MsSqlEf6`, `Rhetos.MSBuild` | 6.0.0 | |

`Rhetos.DslParser` 6.0.0 ships `Rhetos.Core.DslParser.dll` (netstandard2.1) and `Rhetos.Core.Integration.dll` (netstandard2.0), and depends on `Newtonsoft.Json` 13.0.3 (`Rhetos6:src/Rhetos.DslParser.nuspec` at tag `v6.0.0`) (V). `RhetosProjectRootPathResolver` uses Newtonsoft without a direct reference. `CodeAnalysis.csproj` references only `Rhetos.DslParser` and the logging packages, so Newtonsoft must come from `Rhetos.DslParser` (V by elimination).

### 3.2 Rhetos types used

| Rhetos assembly (source folder) | Types and members used | Used in |
|---|---|---|
| `Rhetos.Core.DslParser` (`Rhetos6:src/Rhetos.Core.DslParser/`, namespace `Rhetos.Dsl`) | `DslSyntax` (`Version`, `RhetosVersion`, `CurrentVersion`, `ConceptTypes`), `DslSyntaxFile` (`DslSyntaxFileName`, `Load`), `DslDocumentation`, `DslDocumentationFile`, `ConceptDocumentation` (`Summary`, `Remarks`) | `Tools/DslSyntaxProvider.cs`, `Services/RhetosProjectContext.cs`, `Services/ConceptQueries.cs`, `test/CommonTestTools/DslSyntaxProviderMock.cs` |
| same | `ConceptType` (`Keyword`, `TypeName`, `AssemblyQualifiedName`, `Members`, `IsAssignableFrom`, `GetKeywordOrTypeName`), `ConceptMemberSyntax` (`Name`, `IsKey`, `IsParentNested`, `IsConceptInfo`, `IsConceptInfoInterface`, `IsStringType`, `IsParsable`, `ConceptType`), `ConceptSyntaxNode.Concept` | `ConceptQueries`, `ConceptTypeTools`, `CodeAnalysisResult`, `RhetosDocument`, `RhetosSignatureHelpHandler` |
| same | `Tokenizer`, `TokenizerInternals` (`SkipWhitespaces`, `GetNextToken_ValueType`), `ITokenizer`, `TokenizerResult`, `Token`, `TokenType`, `DslParser` (ctor, 3 events, `GetConcepts`), `ITokenReader`, `TokenReader.PositionInTokenList`, `DslSyntaxException` (`ErrorCode`, `FilePosition`) | `Parsing/CodeAnalysisRun.cs`, `Parsing/TokenizerExplicitTokens.cs`, `Parsing/CodeAnalysisResult.cs` |
| same | `IDslScriptsProvider`, `DslScript`, `IExternalTextReader` | `Parsing/TextDocument.cs`, `Parsing/SimpleExternalResourceReader.cs` |
| `Rhetos.Core.Integration` (`Rhetos6:src/Rhetos.Core.Integration/`) | `Rhetos.Utilities.RhetosBuildEnvironment` (only `CacheFolder`), `ValueOrError<T>`, `FilePosition` | `DslSyntaxProvider`, `SimpleExternalResourceReader`, `CodeAnalysisRun` |
| same | `Rhetos.Logging.ILogProvider`, `ILogger`, `EventType` | `Tools/RhetosNetCoreLogProvider.cs`, `Tools/RhetosNetCoreLogger.cs` |

### 3.3 Reused, wrapped or rewritten

| Area | Status | Detail |
|---|---|---|
| Lexer | Reused | `new Tokenizer(textDocument, new SimpleExternalResourceReader(), Lazy<DslSyntax>)`. `GetTokens()` returns partial tokens plus `SyntaxError`. Rhetos documents this partial-result behavior for IntelliSense in `ITokenizer.cs` (V) |
| Comment tokens | Rewritten (small loop) | Rhetos `Tokenizer.GetTokens` drops comments (`if (t.Type != TokenType.Comment)`). `CodeAnalysisRun.ParseCommentTokens` repeats that loop with the public `TokenizerInternals` to collect comments. Unlike Rhetos, it does not set `Token.PositionEndInDslScript`, so the range test in `ApplyCommentsToResult` (`targetPos < PositionEndInDslScript`) can never match. Comment detection then relies only on the "comment starts on the cursor line" fallback (V by reading). Effect on cursors inside multi-line block comments is untested (I) |
| Parser | Reused + event hooks | `new DslParser(new TokenizerExplicitTokens(tokens), Lazy<DslSyntax>, logProvider)`. The cursor keyword, concept context and member reads come from the `OnKeyword`/`OnMemberRead`/`OnUpdateContext` events. The code casts `ITokenReader` to the concrete `TokenReader` to read `PositionInTokenList` (V). The parser stops at the first `DslSyntaxException`, so each document gets at most one parser diagnostic plus one tokenizer diagnostic (I, from exception flow) |
| Error positions | Wrapped | `DslSyntaxException.FilePosition` (1-based) → `LineChr` (0-based). The Rhetos `ErrorCode` (e.g. `RH0008`) becomes the diagnostic code (V) |
| Syntax model | Reused | `DslSyntax`/`ConceptType` come from Rhetos deserialization, and nothing is copied (V) |
| Completion/signature rules | Own code | `ConceptQueries.ValidConceptsForParent`: the parent member is the `IsParentNested` member, else the first member if it is a key concept reference. A concept is valid if that member is `IConceptInfo` or its type is assignable from the parent. `ConceptTypeTools.SignatureDescription` builds the signature text (V) |
| Copied Rhetos source | None found | `RhetosJsonRpcReceiver.GetRenor` copies OmniSharp, not Rhetos (I: its style looks decompiled) |

## 4. LSP features (OmniSharp.Extensions.LanguageServer 0.19.9)

| LSP method | Handler | CodeAnalysis entry point | Notes (V) |
|---|---|---|---|
| `initialize` / `initialized` | `RhetosLanguageServer.OnInitializeAsync` / `OnInitializedAsync` | none | Turns off `dynamicRegistration` for sync, hover, signature help and completion. Sync kind `Full`. Starts the three loops |
| `textDocument/didOpen`, `didChange` | `TextDocumentHandler` | `RhetosWorkspace.UpdateDocumentText` | Full text, first content change only |
| `textDocument/didClose` | `TextDocumentHandler` | `RhetosWorkspace.CloseDocument` | Empty diagnostics published on the next cycle |
| `textDocument/didSave` | `TextDocumentHandler` | none (no-op) | Registers `IncludeText = true` |
| `textDocument/completion` | `RhetosCompletionHandler` | `RhetosDocument.GetCompletionKeywordsAtPosition`, `ConceptQueries.ValidConceptsForParent`, `GetFullDescription` (item `Detail`) | Trigger `.`. Empty `AllCommitCharacters` (CHANGELOG 2.3.0). At parameter positions it returns non-keyword words of the document |
| `completionItem/resolve` | `RhetosCompletionHandler` | none | Returns the item unchanged |
| `textDocument/hover` | `RhetosHoverHandler` | `RhetosDocument.GetHoverDescriptionAtPosition` → `ConceptQueries.GetFullDescription` | Returns an empty `MarkupContent` instead of `null` (VS 2019 compatibility). Plain text, signature + docs + "defined by <AssemblyQualifiedName>" |
| `textDocument/signatureHelp` | `RhetosSignatureHelpHandler` | `RhetosDocument.GetSignatureHelpAtPosition`, `ConceptQueries.GetSignaturesWithDocumentation`, `CodeAnalysisResult.GetValidConceptsWithActiveParameter` | Triggers `.` and space. `ActiveSignature`/`ActiveParameter` = 100 when unknown |
| `textDocument/publishDiagnostics` (server → client) | `PublishDiagnosticsRunner` | `RhetosDocument.GetAnalysis().AllErrors` | Polled every 300 ms |
| `shutdown` / `exit` | `RhetosLanguageServer.AddServerCleanupHandlers` | none | Stops the loops |

All handlers use the document selector language `rhetos-dsl`. `TextDocumentHandler.GetTextDocumentAttributes` returns the language id `"csharp"`; the effect of this was not verified. Not implemented: definition, references, symbols, formatting, semantic tokens, `workspace/didChangeWatchedFiles`, configuration (V).

Client side (V):

| Item | Value | Source |
|---|---|---|
| File type | Content type `rhe` (base `CodeRemoteContentDefinition`), extension `.rhe` | `src/VisualStudioExtensionShared/RheContentDefinition.cs` |
| Server launch | `rhetos.lsp-server.local.json` `ServerPath` (relative to the VS process working directory). Otherwise `HKLM\SOFTWARE\Rhetos\RhetosLanguageServices` (or `WOW6432Node`) value `Location` + `Rhetos.LanguageServices.Server.exe`, written by the MSI | `LanguageClient.cs`, `src/InstallerCustomActions/CustomActions.cs` |
| Highlighting | TextMate grammar with a fixed keyword list, `fileTypes` `.rhe`, `.alpha` | `src/VisualStudioExtensionSharedFiles/Grammars/Rhetos.json` |
| Rhetos build output used | Compares the `obj/Rhetos/RhetosGeneratedSourceFiles.txt` timestamp before and after a build. If it changed, touches `obj/project.assets.json` to force a design-time build | `SolutionRefreshService.cs` |
| Projects | VS 2019: `src/VisualStudioExtension` (`VisualStudioSdk16`). VS 2022: `src/VisualStudioExtension17`. Both use the shared sources in `VisualStudioExtensionShared.projitems` | csproj files |

## 5. Tests

### 5.1 Projects

| Project | Contents | How it gets a Rhetos app (V) |
|---|---|---|
| `test/CommonTestTools` | `DslSyntaxProviderMock` reads `DslSyntax.json` directly from a folder (`CacheFolder = folder`, no `obj/Rhetos`). `RootPathResolverMock` always returns the context root. `TestCommon.CreateTestServiceProvider(folder)` registers the same DI services as the server and initializes the context with the mock. `IsTestProject` is `false`, because the project references `Microsoft.NET.Test.Sdk`, and `dotnet test Rhetos.LanguageServices.Server.slnf` should run only the two test projects | Mock provider |
| `test/RhetosMockAppContents` | Content-only project: `DslSyntax.json`, `DslDocumentation.json`, `RhetosAppFolder/**` fixtures, all `CopyToOutputDirectory` | Supplies the files |
| `test/CodeAnalysis.Test` | `CodeAnalysisTests`, `ConceptQueriesTests`, `ConceptTypeToolsTests`, `ProjectRootPathResolverTests`, `RhetosDocumentTests`, `RhetosWorkspaceTests`, `TextDocumentTests` (MSTest 3.10.4) | `[DeploymentItem("DslSyntax.json")]` + `CreateTestServiceProvider("./")`. The resolver tests deploy `DslSyntax.json` to `RhetosAppFolder\FolderWithApp\obj\Rhetos\` |
| `test/LspIntegration.Test` | `IntegrationTestBase` (in-memory client/server through OmniSharp `LanguageProtocolTestBase` + `RhetosLanguageServer.ConfigureLanguageServer`), `BasicOperationsTests`, `DocumentUriTests`, `ProjectContextTests`, `ServerLifecycleTests`, `MemoryLoggerProvider`. `DocumentUriWireTests` instead starts the server process and speaks raw JSON-RPC ([omnisharp-document-uri.md §7](../lsp/omnisharp-document-uri.md#7-testing-notes)) | `DeploymentItem` copies `DslSyntax.json` into `FolderWithApp(2)\obj\Rhetos\`, so the real resolver and `DslSyntaxProvider` are used. Tests wait 1–1.5 s for the polling loops |
| `test/RhetosTestApp` | Real Rhetos 6 app project (csproj only, no `.rhe` tracked) referencing `RhetosTestAppConcepts` | Not referenced by any test. A local build has produced `obj/Rhetos/DslSyntax.json` (DSL 6.0) and `rhetos-project.assets.json` (untracked). Probably used for manual testing and for regenerating the mock (I) |
| `test/RhetosTestAppConcepts` | `WithParentInfo`: keyword `WithParent`, keys `DataStructure` + `Name`, `[ConceptParent] ModuleParent`, `Parameter`. Used to test `ConceptParentAttribute` completion | Compiled into `RhetosTestApp` |

The mock `test/RhetosMockAppContents/DslSyntax.json` has `"Version": "5.0"` and `"RhetosVersion": "5.0.0-dev220309103387d0b6"`. It contains `RhetosTestAppConcepts.WithParentInfo`, and its CommonConcepts types are named `Rhetos.Dsl.DefaultConcepts, Version=5.0.0.0`. The Rhetos 6 build output names them `Rhetos.CommonConcepts` (V). The mock was therefore generated from `RhetosTestApp` on a Rhetos 5 build and has not been regenerated for Rhetos 6 (I). No `rhetos-project.assets.json` is tracked under `test/` (V, `git ls-files`).

Building `RhetosTestApp` (needed only to regenerate the mock): the build runs the Rhetos 6.0.0 CLI `tools/rhetos.dll` from the `Rhetos.MSBuild` package, whose `rhetos.runtimeconfig.json` requires the .NET 8 and ASP.NET Core 8 runtimes (V). The `RollForward` setting of this repository's projects does not apply to the CLI. The MSBuild property `RhetosBuildDotNetRollForward` passes `--roll-forward` to it ([build-and-project-layout.md §2.1](build-and-project-layout.md#21-msbuild-targets-v5v6-rhetosmsbuildtargets-is-identical-in-gitv600-and-the-working-tree)); whether the 6.0.0 CLI runs on .NET 10 this way is not tested. For this reason `RhetosTestApp` and `RhetosTestAppConcepts` are not in `Rhetos.LanguageServices.Server.slnf`.

### 5.2 Fixtures in `test/RhetosMockAppContents/RhetosAppFolder/`

| Folder | Scenario | Expected (test) |
|---|---|---|
| `FolderWithApp`, `FolderWithApp/SubFolder` | Detection through the deployed `obj/Rhetos/DslSyntax.json` | `DetectedRhetosApp` |
| `FolderWithApp2` | Second app. Used for switching and for the timestamp-reload test | Context switches or reinitializes |
| `FolderWithAppUnsupported/MockObj/Rhetos/DslSyntax.json` | `Version 1111.0`, deployed to `obj/Rhetos` | Version error diagnostic, 0 completions |
| `FolderWithConfiguration` (+`SubFolder`) | Settings file → `c:\FolderFromJson\Rhetos` | `ConfigurationFile` |
| `FolderWithConfiguration/FaultyConfiguration` (+`FaultySubFolder`) | Settings file without the key. `directive.rhe` overrides it | `None`; directive → `SourceDirective` |
| `EmptyFolder` | No app. Directive after blank lines. `fileinclude.rhe` + `include.txt` | `None` / `SourceDirective` |
| `RhetosAppDynamic` | The test creates `obj/Rhetos/DslSyntax.json` (`{ "ConceptTypes": [] }`) at runtime | Initializes within 1.5 s |

`.gitignore` ignores `[Oo]bj/`, so the fixtures are stored as `MockObj` and remapped by `[DeploymentItem]` (V for the rule; I for the intent). `RhetosMockAppContents.csproj` still lists `FolderWithAppUnsupported\obj\Rhetos\DslSyntax.json`, a file that is not tracked (V).

### 5.3 Running tests (not run here)

| Method | Source | Status |
|---|---|---|
| Open `Rhetos.LanguageServices.sln` in VS 2022, build, run unit tests | `README.md` "Building and testing the source code" | Documented |
| `test.bat` (runs `clean.bat`, `build.bat`, then `vstest.console.exe` on `*.Test.dll`) | `test.bat` | Needs Visual Studio and `NuGet.exe`; for the server and its tests without Visual Studio use `tools/build-server.ps1` |
| `dotnet test test/CodeAnalysis.Test` / `test/LspIntegration.Test`, or `tools/build-server.ps1` (builds `Rhetos.LanguageServices.Server.slnf`, tests, publishes to `dist/server`) | `README.md` "Building and testing the source code" | Documented. Both are SDK-style net8.0 MSTest projects with no VSIX dependency; `RollForward` is `LatestMajor`, so a newer installed .NET runtime also works |

## 6. Known limitations and TODOs related to Rhetos integration

| # | Item | Source | Kind |
|---|---|---|---|
| 1 | README detection rule names `rhetos-project.assets.json`, but the code uses `DslSyntax.json` | `README.md`, `Tools/DslSyntaxProvider.cs` | Doc drift, README corrected 2026-10-02 (V) |
| 2 | README and the in-editor warning say DLLs are loaded and VS must be restarted. The code loads only JSON and switches apps once the first app's documents are closed | `README.md`, `RhetosDocument.BlockedAnalysisResult`, test `WillSwitchOnUnusedProjectPath` | Doc drift, README corrected 2026-10-02 (V) |
| 3 | README says the directive must be on the first line. The regex accepts it after leading whitespace or blank lines | `RhetosProjectRootPathResolver.GetRootPathFromText` | Doc drift, README corrected 2026-10-02 (V) |
| 4 | Apps with a DSL version above 6.0 are rejected until a new release is built against a newer `Rhetos.DslParser` | `RhetosProjectContext.cs` | Limitation (V) |
| 5 | Load failures (bad JSON, missing `ConceptTypes`) are logged at Trace. The default NLog level is Info, so the user sees only "Waiting for Rhetos Project initialization." | `RhetosWorkspace.TryInitializeWithRootPaths`, `src/Server/NLog.config` | V; user impact I |
| 6 | Only the `DslSyntax.json` timestamp triggers a reload. Rhetos rewrites that file only when its content changes (`DslSyntaxFile.Save`), so a change that affects only `DslDocumentation.json` is probably not picked up | `RhetosProjectContext.UpdateDslSyntax`, `Rhetos6:src/Rhetos.Core.DslParser/DslSyntaxFile.cs` | I |
| 7 | Change detection uses polling (1 s context, 300 ms diagnostics), not a file watcher | `Services/RhetosProjectMonitor.cs`, `PublishDiagnosticsRunner.cs` | V |
| 8 | Referenced external files and SQL resources are never read, so their content is not checked | `Parsing/SimpleExternalResourceReader.cs`, `RhetosDocumentTests.ScriptWithFileInclude` | V |
| 9 | One static lock serializes the analysis of all documents. Comment: "we don't want to run analysis during document text change and also rely on Rhetos parsing infrastructure to be thread-safe" | `Parsing/RhetosDocument.cs` (`_syncAnalysis`) | V |
| 10 | Comment-token end positions are not set (section 3.3) | `CodeAnalysisRun.ParseCommentTokens` | V; effect I |
| 11 | The keyword list for highlighting is fixed and does not follow the app's `DslSyntax.json`. Only open documents are analyzed | `README.md` Limitations | V |
| 12 | `// TODO: this class is maybe specific for LanguageServices` | `Services/RhetosWorkspace.cs` | TODO (V) |
| 13 | `// HACK: Force the "design-time build", by updating timestamp on project.assets.json` | `SolutionRefreshService.cs` | Hack (V) |
| 14 | OmniSharp workarounds: `RhetosJsonRpcReceiver` (issue #609), log-filter rules, disabling dynamic registration for VS 2022 | `src/Server/RhetosLanguageServer.cs` | V |
| 15 | Fixed in version 6.1.0 (unreleased): the resolver returns None with a reason for non-file documents (for example `untitled:`) and no longer resolves a relative directive against the server's working directory. Before, `Path.GetDirectoryName(LocalPath)` was empty for them | `RhetosProjectRootPathResolver.cs` | V (live check 2026-10-02) |
| 16 | `test/CodeAnalysis.Test` references the full `Rhetos` 6.0.0 package without visible direct use | `test/CodeAnalysis.Test/CodeAnalysis.Test.csproj` | I |
