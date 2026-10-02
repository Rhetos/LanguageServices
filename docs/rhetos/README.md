# Rhetos background for Rhetos Language Services

Rhetos is a .NET framework in which an application declares its data structures and business rules in a DSL, in `.rhe` script files.
Each DSL keyword ("concept") is a C# class that implements `IConceptInfo` and is shipped in a plugin NuGet package; the keyword's parameters are the class's public settable properties.
A `dotnet build` of a Rhetos application runs `rhetos build` (MSBuild target `BuildRhetosApp`), which scans the plugin DLLs and writes the application's complete DSL grammar to `obj/Rhetos/DslSyntax.json` and the concept descriptions to `obj/Rhetos/DslDocumentation.json`.
This language server reads only those two JSON files and never loads the application's DLLs.
It parses each open `.rhe` document with the Rhetos tokenizer and parser from the `Rhetos.DslParser` NuGet package (6.0.0), so it applies the same parsing rules as the Rhetos build of that package version.

Read this page first. Then read the document from the index that covers your task.

## Terms

| Term | Meaning |
|---|---|
| Concept | One DSL statement type: a C# class that implements `IConceptInfo`. In `DslSyntax.json` it is one `ConceptType` |
| Keyword | First token of a statement, from `[ConceptKeyword]`. Case-insensitive; several concepts may share one keyword |
| Member | One concept parameter: a public settable property. In `DslSyntax.json` it is one `ConceptMemberSyntax` |
| Key member | Member with `[ConceptKey]` (`IsKey`). Key members form the concept's unique key and are the only members written when the concept is referenced |
| Parent member | Member that the enclosing `{ }` statement fills when the statement is nested |
| Reference | Member whose type is another concept. Written as the referenced concept's key values separated by `.` |
| Plugin package | Any NuGet package or referenced project whose DLLs contain `[Export(typeof(IConceptInfo))]` classes. There is no marker or naming rule |
| `Rhetos.CommonConcepts` | The standard concept library: `Module`, `Entity`, `ShortString`, `Reference`, and others |
| Macro | Plugin that creates more concepts after parsing (`IConceptMacro<T>`, `IMacroConcept`). Macro-created concepts do not exist at parse time |
| `rhetos build` | CLI step run by MSBuild. Generates `DslSyntax.json`, `DslDocumentation.json`, C# sources and assets. Needs no database |
| `rhetos dbupdate` | CLI step after the build that updates the database. The language server does not depend on it |

## Documents

| Document | What it answers | Read it when |
|---|---|---|
| [dsl-syntax.md](dsl-syntax.md) | How the Rhetos tokenizer and parser work: token rules, positions, statement grammar, nesting, overload disambiguation, error codes RH0001–RH0013, script discovery, version changes | You change parsing, diagnostics, positions or cursor-context logic |
| [concepts-and-plugins.md](concepts-and-plugins.md) | How a C# `IConceptInfo` class becomes a DSL keyword: attributes, member rules, inheritance, other plugin interfaces, plugin discovery, XML docs, Rhetos 4/5/6 differences | You need to know why a keyword has its parameters, parent or overloads |
| [dsl-syntax-json.md](dsl-syntax-json.md) | Schema, serialization and generation of `DslSyntax.json` and `DslDocumentation.json`; version history; what an editor can and cannot get from the files | You read, mock or reload these files, or handle a DSL syntax version |
| [build-and-project-layout.md](build-and-project-layout.md) | Rhetos app layout on disk, `obj/Rhetos/` inventory, `rhetos-project.assets.json` schema, MSBuild targets, Rhetos 4/5/6 version matrix, NuGet packages | You work on app detection, file watching, or support for a Rhetos version |
| [language-services-integration.md](language-services-integration.md) | How this repo uses Rhetos: component map, app detection, files read, one app per process, Rhetos types used, LSP features, tests and fixtures, limitations | Before you change code in `src/` or `test/` |
| [common-concepts.md](common-concepts.md) | The standard keywords of `Rhetos.CommonConcepts` by category, with real DSL script examples | You need realistic DSL samples or expectations for completion and hover tests |

Where to start for common tasks (code paths without a `src/` prefix are relative to `src/CodeAnalysis/`):

| Task | Code | Docs |
|---|---|---|
| Diagnostics and error ranges | `Parsing/CodeAnalysisRun.cs`, `src/Server/Services/PublishDiagnosticsRunner.cs` | [dsl-syntax.md §3](dsl-syntax.md#3-error-reporting), [language-services-integration.md §3.3](language-services-integration.md#33-reused-wrapped-or-rewritten) |
| Completion, signature help, hover | `Services/ConceptQueries.cs`, `Parsing/ConceptTypeTools.cs`, `Parsing/RhetosDocument.cs`, `src/Server/Handlers/` | [concepts-and-plugins.md §8](concepts-and-plugins.md#8-relevant-for-language-services), [dsl-syntax-json.md §8](dsl-syntax-json.md#8-relevant-for-language-services) |
| Rhetos app detection | `Services/RhetosProjectRootPathResolver.cs`, `Tools/DslSyntaxProvider.cs` | [language-services-integration.md §2.1](language-services-integration.md#21-root-path-resolution-per-document-v-srccodeanalysisservicesrhetosprojectrootpathresolvercs), [build-and-project-layout.md §6](build-and-project-layout.md#6-relevant-for-language-services) |
| Loading and reloading `DslSyntax.json` | `Services/RhetosProjectContext.cs`, `Services/RhetosWorkspace.cs`, `src/Server/Services/RhetosProjectMonitor.cs` | [language-services-integration.md §2.2](language-services-integration.md#22-files-read-from-the-rhetos-app-v), [dsl-syntax-json.md §5](dsl-syntax-json.md#5-generation-during-dotnet-build) |
| A new editor client | `src/Server/RhetosLanguageServer.cs` (LSP over stdio), `src/VisualStudioExtensionShared/LanguageClient.cs` (existing client) | [language-services-integration.md §1.1](language-services-integration.md#11-main-flow-v), [§4](language-services-integration.md#4-lsp-features-omnisharpextensionslanguageserver-0199) |
| Test fixtures | `test/RhetosMockAppContents/`, `test/RhetosTestApp/`, `test/CommonTestTools/` | [language-services-integration.md §5](language-services-integration.md#5-tests) |

## Most important facts for this project

### Build output: the two JSON files

| Fact | See |
|---|---|
| Both files are in the app's `CacheFolder`, normally `<app>/obj/Rhetos/`. Rhetos hard-codes `obj` in other places, so a custom intermediate path is effectively unsupported [I]. | [dsl-syntax-json.md §1](dsl-syntax-json.md#1-quick-facts), [build-and-project-layout.md §6](build-and-project-layout.md#6-relevant-for-language-services) |
| Only `rhetos build` writes them (target `BuildRhetosApp`, before `CoreCompile`). Their generators run first, so the files are written even when `.rhe` scripts have errors. The Rhetos runtime and `rhetos dbupdate` do not use them. | [dsl-syntax-json.md §5](dsl-syntax-json.md#5-generation-during-dotnet-build) |
| Each file is rewritten only when its content changes. Editing only `.rhe` files leaves `DslSyntax.json` untouched, so a timestamp change means a new grammar. | [dsl-syntax-json.md §3](dsl-syntax-json.md#3-serialization-details-dslsyntaxfile) |
| A Visual Studio design-time build skips `BuildRhetosApp`, so it never creates or updates `DslSyntax.json`. It does write `rhetos-project.assets.json`. | [build-and-project-layout.md §2.1](build-and-project-layout.md#21-msbuild-targets-v5v6-rhetosmsbuildtargets-is-identical-in-gitv600-and-the-working-tree) |
| `rhetos build` needs no database. `dotnet build -p:RhetosDeploy=false` should regenerate the files without one [I]. | [build-and-project-layout.md §2.4](build-and-project-layout.md#24-database-requirement-per-step) |
| `DslSyntax.Version` per Rhetos version: v4 with the `Rhetos.LanguageServicesCompatibility` package → `5.0` (v4 without it: no file); v5 → `5.0`; v6 → `6.0`. Only these two values ever existed; block comments (5.4) did not bump it. | [dsl-syntax-json.md §7](dsl-syntax-json.md#7-history-and-backward-compatibility), [build-and-project-layout.md §4](build-and-project-layout.md#4-version-matrix) |
| `rhetos-project.assets.json` `ContentFiles` is the authoritative list of the app's `.rhe` files. Package scripts must be under `DslScripts\`; project scripts may be anywhere under the project folder (5.3+). This repo does not read the file. | [build-and-project-layout.md §1.3](build-and-project-layout.md#13-rhetos-projectassetsjson-schema), [dsl-syntax.md §4](dsl-syntax.md#4-script-discovery-v) |

### JSON format

| Fact | See |
|---|---|
| Newtonsoft.Json 13.0.3 with `PreserveReferencesHandling.Objects`: every object has `$id`; a repeated `ConceptType` is `{"$ref": "N"}`, even in the top-level `ConceptTypes` list. Read with `DslSyntaxFile.Load`, or resolve `$id`/`$ref` yourself. | [dsl-syntax-json.md §3](dsl-syntax-json.md#3-serialization-details-dslsyntaxfile) |
| Root properties: `Version` (string, e.g. `"6.0"`), `RhetosVersion` (information only), `ExcessDotInKey` (0/1/2), `DatabaseLanguage`, `ConceptTypes` (sorted by `AssemblyQualifiedName`). | [dsl-syntax-json.md §2.1](dsl-syntax-json.md#21-dslsyntax-root-object-srcrhetoscoredslparserdslsyntaxcs) |
| Concept identity is `AssemblyQualifiedName`, which contains assembly name and version (v5 `Rhetos.Dsl.DefaultConcepts`, v6 `Rhetos.CommonConcepts`). Match by `TypeName` across builds. | [dsl-syntax-json.md §8](dsl-syntax-json.md#8-relevant-for-language-services), [concepts-and-plugins.md §7](concepts-and-plugins.md#7-version-differences-4--5--6) |
| `DslDocumentation.json` = `{ "Concepts": { "<AssemblyQualifiedName>": { "Summary", "Remarks" } } }`. Only type-level XML docs, whitespace flattened. No member docs, no `[Obsolete]`. Hover is therefore type-level only. | [dsl-syntax-json.md §4](dsl-syntax-json.md#4-companion-file-dsldocumentationjson), [concepts-and-plugins.md §6](concepts-and-plugins.md#6-xml-documentation) |

### Concept classes (`IConceptInfo`)

| Fact | See |
|---|---|
| The keyword comes only from `[ConceptKeyword]`, read with `inherit: false`; a derived class needs its own. No name derivation. `Keyword = null` means the concept cannot start a statement but can be referenced. | [concepts-and-plugins.md §1](concepts-and-plugins.md#1-iconceptinfo-and-the-metadata-attributes) |
| Keywords are case-insensitive (`OrdinalIgnoreCase`). One keyword can map to several concept classes (overloads). | [dsl-syntax.md §2.1](dsl-syntax.md#21-shape) |
| Overloads: the parser tries all of them; the interpretation that consumes the most tokens wins, then a concrete parent type over an `IConceptInfo` parent, then the lowest nesting depth. A tie gives RH0006. | [dsl-syntax.md §2.4](dsl-syntax.md#24-disambiguation-of-same-keyword-concepts-v-dslparserparsenextconcept-disambiguate) |
| Members are public settable instance properties, base class first, then declaration order (`Index`). Types: `string`, `IConceptInfo`, or a concept class. Every parsable member is a required positional parameter. | [concepts-and-plugins.md §1.1](concepts-and-plugins.md#11-members-conceptmembersget) |
| Non-parsable members (`IsParsable = false`, from `IAlternativeInitializationConcept`) are skipped in the statement, but stay in `Members` and can be key members. | [concepts-and-plugins.md §4.1](concepts-and-plugins.md#41-ialternativeinitializationconcept-and-parsing) |
| Key members (`IsKey`) identify a concept. A reference to a concept reads only its key members, dot-separated (`Bookstore.Book`). A derived class cannot add key members. | [concepts-and-plugins.md §3](concepts-and-plugins.md#3-derived-inherited-concepts), [dsl-syntax.md §2.3](dsl-syntax.md#23-reference-parameters-v) |
| Parent member: the last member with `[ConceptParent]` (`IsParentNested`), else the first member if it references a concept, else none (root level only). When nested, its value comes from the enclosing statement and no tokens are read. | [dsl-syntax.md §2.2](dsl-syntax.md#22-nesting-and-the-parent-parameter-v) |
| The legacy dot before a string key (`Entity Demo.Book;`) still parses, with an "Obsolete syntax" warning whose severity `ExcessDotInKey` sets. Current form: `Entity Demo Book;`. | [dsl-syntax.md §2.3](dsl-syntax.md#23-reference-parameters-v) |

### Tokenizer, positions and errors

| Fact | See |
|---|---|
| A Text token is an identifier (letters, digits, `_`), a quoted string (`'…'` or `"…"`, quote escaped by doubling, may span lines), or a `<file>` reference whose value is the file content. The parser cannot tell them apart. | [dsl-syntax.md §1.1](dsl-syntax.md#11-token-rules-v) |
| Comments: `//` to end of line, and `/* */` (not nested, Rhetos 5.4+). `Tokenizer.GetTokens` drops them. | [dsl-syntax.md §1.1](dsl-syntax.md#11-token-rules-v) |
| Token offsets are 0-based UTF-16 (end exclusive). `FilePosition` line and column are 1-based. Only `\n` breaks a line; a tab is 1 column. LSP position = (Line − 1, Column − 1). | [dsl-syntax.md §1.3](dsl-syntax.md#13-positions-v) |
| No error recovery: the tokenizer and the parser stop at the first error, so a document gets at most one tokenizer and one parser diagnostic [I]. | [dsl-syntax.md §3](dsl-syntax.md#3-error-reporting), [language-services-integration.md §3.3](language-services-integration.md#33-reused-wrapped-or-rewritten) |
| Error codes RH0001–RH0013 (RH0011 removed in 6.0). RH0003, RH0004 and RH0006 cover only the keyword token. RH0007 points at the token after `}`. RH0010 has End = (1,1), an inverted range that must be clamped. | [dsl-syntax.md §3.2](dsl-syntax.md#32-error-codes-v) |
| Semantic errors (unresolved reference, duplicate key, concept validation) come after parsing from `DslContainer`/`DslModel` and have no file position. The parser does not check that a referenced concept exists. | [dsl-syntax.md §3.1](dsl-syntax.md#31-exception-types-v) |

### This repository

| Fact | See |
|---|---|
| App root detection: rule 1 directive `// <rhetosProjectRootPath="..." />` (blank lines may precede it), then rule 2 `rhetos-language-services.settings.json`, then rule 3: the first folder upwards with `obj/Rhetos/DslSyntax.json` (`DslSyntaxProvider.IsValidProjectRootPath`). | [language-services-integration.md §2.1](language-services-integration.md#21-root-path-resolution-per-document-v-srccodeanalysisservicesrhetosprojectrootpathresolvercs) |
| The server reads only `DslSyntax.json`, `DslDocumentation.json` and the settings file. It has no `Assembly.Load*`/`AssemblyLoadContext`, does not read the assets file or `<file>` references, and gets `.rhe` text only from LSP. | [language-services-integration.md §2.2](language-services-integration.md#22-files-read-from-the-rhetos-app-v) |
| Version check: `DslSyntax.Version` null or greater than `DslSyntax.CurrentVersion` (6.0 in `Rhetos.DslParser` 6.0.0) gives an initialization error on every document. Files with 5.0 and 6.0 load. | [language-services-integration.md §2.2](language-services-integration.md#22-files-read-from-the-rhetos-app-v) |
| One app per process: one `RhetosProjectContext`. Documents of another app get a warning and no analysis. The context switches automatically once all documents of the current app are closed (test `WillSwitchOnUnusedProjectPath`). Up to v1.0.0 the limit came from loaded DLLs. | [language-services-integration.md §2.3](language-services-integration.md#23-only-one-rhetos-app-per-process) |
| Polling, no file watcher: context check every 1 s (reload when the `DslSyntax.json` timestamp changes), diagnostics every 300 ms. A change only in `DslDocumentation.json` is probably not reloaded [I]. | [language-services-integration.md §1.1](language-services-integration.md#11-main-flow-v), [§6](language-services-integration.md#6-known-limitations-and-todos-related-to-rhetos-integration) |
| Reused from Rhetos: `Tokenizer`, `DslParser` (events `OnKeyword`, `OnMemberRead`, `OnUpdateContext`), `DslSyntaxFile`, `DslDocumentationFile`. `SimpleExternalResourceReader` stubs `<file>` references. | [language-services-integration.md §3.3](language-services-integration.md#33-reused-wrapped-or-rewritten) |
| Rhetos packages: `src/CodeAnalysis` (net8.0) references only `Rhetos.DslParser` 6.0.0. `test/CodeAnalysis.Test` references `Rhetos` 6.0.0; `test/RhetosTestApp` references `Rhetos.CommonConcepts`, `Rhetos.MsSqlEf6`, `Rhetos.MSBuild` 6.0.0. LSP library: OmniSharp 0.19.9. | [language-services-integration.md §3.1](language-services-integration.md#31-package-references-v-from-csproj-files) |
| Test fixture `test/RhetosMockAppContents/DslSyntax.json` is stale: `Version` 5.0, Rhetos 5.0.0-dev, v5 `AssemblyQualifiedName`s. `FolderWithAppUnsupported` has `Version` 1111.0. `obj` fixtures are stored as `MockObj`, because git ignores `obj/`. | [language-services-integration.md §5.1](language-services-integration.md#51-projects), [§5.2](language-services-integration.md#52-fixtures-in-testrhetosmockappcontentsrhetosappfolder) |
| Syntax highlighting uses a fixed TextMate keyword list (`src/VisualStudioExtensionSharedFiles/Grammars/Rhetos.json`), not `DslSyntax.json`. | [language-services-integration.md §4](language-services-integration.md#4-lsp-features-omnisharpextensionslanguageserver-0199) |

## Known discrepancies

Places where documentation or fixtures disagree with the code. The root `README.md` discrepancies found during the research were corrected on 2026-10-02 and are no longer listed.

| # | Where | What it says | What the code does | See |
|---|---|---|---|---|
| D1 | In-editor warning, `RhetosDocument.BlockedAnalysisResult` | "Restart Visual Studio if you want to use a different Rhetos app." | Closing the current app's documents is enough; the server loads no DLLs | [language-services-integration.md §2.3](language-services-integration.md#23-only-one-rhetos-app-per-process) |
| D2 | Wiki `Rhetos-DSL-syntax.md` | Flat form `ShortString Demo.School.Name;` | Legacy dot syntax (obsolete since 3.1, parses with a warning). Current: `ShortString Demo.School Name;` | [dsl-syntax.md §2.3](dsl-syntax.md#23-reference-parameters-v) |
| D3 | Wiki `Rhetos-concept-development.md`, `Creating-a-Rhetos-package.md` | Concepts defined in `Rhetos.Dsl.Interfaces`/`Rhetos.Compiler.Interfaces`; sample nuspec `net472`, Rhetos 2.0 | v6 uses `Rhetos.Core.DslParser.dll` and `Rhetos.Core.dll`, net10.0 (working tree) | [concepts-and-plugins.md §7](concepts-and-plugins.md#7-version-differences-4--5--6) |
| D4 | Wiki `Rhetos-CLI.md` | `rhetos dbupdate <folder>` | v5+: `rhetos dbupdate <App>.dll` | [build-and-project-layout.md §2.3](build-and-project-layout.md#23-what-rhetos-dbupdate-does-brief) |
| D5 | Wiki `Creating-a-new-application-with-Rhetos-framework.md`, `Build-binaries-from-source.md` | No DB provider package; build output in `Install` | v6 build fails without a DB provider; the Rhetos repo builds into `dist` | [build-and-project-layout.md §6](build-and-project-layout.md#6-relevant-for-language-services) |
| D6 | Wiki (all pages) | Nothing about `DslSyntax.json` or `DslDocumentation.json` | Both files exist since Rhetos 5 (gap, not a contradiction) | [dsl-syntax-json.md §1](dsl-syntax-json.md#1-quick-facts) |
| D7 | Rhetos `ConceptKeywordAttribute` | `AttributeUsage(Inherited = true)` | `GetKeyword` reads with `inherit: false`; the keyword is not inherited | [concepts-and-plugins.md §1](concepts-and-plugins.md#1-iconceptinfo-and-the-metadata-attributes) |
| D8 | Rhetos `ChangeLog.md` 5.6.0 | Refers to a "6.1.0" release | The next heading is "6.1.1 (TO BE RELEASED)" | [build-and-project-layout.md §6](build-and-project-layout.md#6-relevant-for-language-services) |
| D9 | `test/RhetosMockAppContents/DslSyntax.json` | `Version` 5.0, Rhetos 5.0.0-dev, v5 type names | The repo targets Rhetos 6 (`Rhetos.DslParser` 6.0.0); a local `RhetosTestApp` build produces `Version` 6.0 | [language-services-integration.md §5.1](language-services-integration.md#51-projects) |
| D10 | `test/RhetosMockAppContents/RhetosMockAppContents.csproj` | Lists `FolderWithAppUnsupported\obj\Rhetos\DslSyntax.json` | That file is not tracked; the fixture is under `MockObj` | [language-services-integration.md §5.2](language-services-integration.md#52-fixtures-in-testrhetosmockappcontentsrhetosappfolder) |
| D11 | `test/CodeAnalysis.Test/CodeAnalysis.Test.csproj` | References the full `Rhetos` 6.0.0 package | No visible direct use [I] | [language-services-integration.md §3.1](language-services-integration.md#31-package-references-v-from-csproj-files) |

Code issues in this repo found during the research (not documentation drift):

| # | Issue | See |
|---|---|---|
| I1 | `CodeAnalysisRun.ParseCommentTokens` does not set `PositionEndInDslScript`, so the range test in `ApplyCommentsToResult` never matches | [language-services-integration.md §3.3](language-services-integration.md#33-reused-wrapped-or-rewritten) |
| I2 | `CodeAnalysisRun` maps `FilePosition.End` directly, so RH0010 produces an inverted range [I] | [dsl-syntax.md](dsl-syntax.md#relevant-for-language-services) |
| I3 | `DslSyntax.json` load failures are logged at Trace only; with the default NLog level (Info) the user sees only "Waiting for Rhetos Project initialization." | [language-services-integration.md §6](language-services-integration.md#6-known-limitations-and-todos-related-to-rhetos-integration) |
| I4 | `TextDocumentHandler.GetTextDocumentAttributes` returns language id `"csharp"`; effect not verified | [language-services-integration.md §4](language-services-integration.md#4-lsp-features-omnisharpextensionslanguageserver-0199) |

## Sources

The research was done on 2026-10-02 against these revisions. Local clones were used; paths in the documents are relative to each repository root.

| Source | GitHub | Revision used | Notes |
|---|---|---|---|
| Rhetos framework | https://github.com/Rhetos/Rhetos | `1c635dc97872f66dca7afce64a51d2d0c616a8da`, 2026-09-14, nearest upstream tag `v6.0.0`. This commit is not in upstream; it exists only in the fork branch `optimize-empty-query`: https://github.com/bantolov/Rhetos/commit/1c635dc97872f66dca7afce64a51d2d0c616a8da | Working tree 6.1.1-dev, targets .NET 10 (`net10.0`). The released 6.0.0 targets .NET 8. `src/Rhetos.Core.DslParser` and `src/Rhetos.Core.Integration` are unchanged since tag `v6.0.0` except csproj/nuspec. Built sample apps: `test/CommonConcepts.TestApp.MsSql` and others |
| Rhetos wiki | https://github.com/Rhetos/Rhetos/wiki (rendered), https://github.com/Rhetos/Rhetos-wiki (git) | `6c95c11`, 2026-04-08, branch `master` | Partly outdated for Rhetos 6 (see Known discrepancies) |
| Rhetos v4 compatibility plugin | https://github.com/Rhetos/LanguageServicesCompatibility | `a591ab4`, 2025-09-08, branch `main` | Writes `DslSyntax.json` (`Version` 5.0) for Rhetos v4 apps |
| Bookstore sample app | https://github.com/Rhetos/Bookstore | `aa024a0`, 2025-09-23, branch `master` (upstream HEAD; the repository has no tags) | Rhetos 6.0.0, net8.0, EF6 (`Rhetos.MsSqlEf6`); its built `obj/Rhetos` was examined. Its `Readme.md` still says "Rhetos v5". Rhetos 4 revision: branch `rhetos-4` (`ae4d5ae`) / `PermissionsRecorder` commit `afa98a5` (2022-07-08, one commit after `rhetos-4`), used for the Rhetos 4 facts (v4 column of the version matrix, compatibility package). Used in `build-and-project-layout.md`, `common-concepts.md`, `concepts-and-plugins.md`, `dsl-syntax-json.md` |
| This repository | https://github.com/Rhetos/LanguageServices | Branch `vs-code` | Git tags `v1.0.0`, `v2.0.3`, `v6.0.0` show the integration history |

Conventions used in the six documents:

| Marker or prefix | Meaning |
|---|---|
| **[V]** (also a `V` column) | Verified in code, a test, git history or a generated file; the source is cited |
| **[I]** (also an `I` column, *(inferred)*, *(unverified)*) | Inference from reading code; no test or direct check |
| Plain paths | Relative to the Rhetos framework repository root (see Sources), except in `language-services-integration.md`, where they are relative to this repo |
| `Rhetos6:` | The Rhetos framework repository root (github.com/Rhetos/Rhetos, see Sources) |
| `LS:`, `LS/`, `LanguageServices:` | This repo |
| `Compat:`, `LSC/` | The LanguageServicesCompatibility repository root (github.com/Rhetos/LanguageServicesCompatibility, see Sources) |
| `Wiki/`, `Wiki\` | The Rhetos wiki repository root (github.com/Rhetos/Rhetos-wiki, see Sources) |
| `Bookstore/`, `Bookstore\`, `Bookstore v4/` | The Bookstore repository root (github.com/Rhetos/Bookstore, see Sources): `Bookstore` at branch `master` (Rhetos 6.0.0), `Bookstore v4` at the Rhetos 4 revision `afa98a5` |
