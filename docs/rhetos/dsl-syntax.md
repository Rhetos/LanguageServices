# Rhetos DSL syntax and parser

Rhetos DSL scripts (`.rhe`) are processed in two stages by the `Rhetos.Core.DslParser` library (netstandard2.1, namespace `Rhetos.Dsl`).
First, `Tokenizer` converts all scripts into one flat list of `Text` and `Special` tokens. It drops comments and appends one `EndOfFile` token per script.
Second, `DslParser` reads statements of the form `keyword parameters (";" | "{" statements "}")`. For each statement it tries every concept type that has the matching keyword (case-insensitive) through `GenericParser`, then selects one interpretation.
The grammar is not fixed. Rhetos builds it at build time from the application's `IConceptInfo` plugin classes (`[ConceptKeyword]`, `[ConceptKey]`, `[ConceptParent]`, property order) and serializes it to `obj\Rhetos\DslSyntax.json` for external tools.
Parsing stops at the first error, which is a `DslSyntaxException` with an `RH00xx` code and a file/line/column range. The parser has no error recovery.
The parser does not check whether a referenced statement (for example `Demo.Region`) exists. `DslContainer` checks that later, and its errors carry no file position.

**Notation:** **[V]** = verified by reading the cited code or test. **[I]** = my inference from tracing code paths; no test covers it, or it depends on .NET runtime behaviour.
All paths are relative to the Rhetos6 repo root. `DslParser/` stands for `src/Rhetos.Core.DslParser/`.

## Source map

| Component | File | Role |
|---|---|---|
| `Token`, `TokenType` | `DslParser/Token.cs` | Types `Text`, `Special`, `Comment`, `EndOfFile`; fields `Value`, `DslScript`, `PositionInDslScript`, `PositionEndInDslScript` |
| `Tokenizer` | `DslParser/Tokenizer.cs` | Loops over all scripts, drops comments, adds EOF per script, returns `TokenizerResult` (tokens + `SyntaxError`) |
| `TokenizerInternals` | `DslParser/TokenizerInternals.cs` | Lexical rules. Public; `GetNextToken_ValueType` also returns comment tokens |
| `TokenReader` | `DslParser/TokenReader.cs` | Cursor: `ReadText`, `TryRead`, `GetPositionInScript`, `SkipEndOfFile` |
| `DslParser` | `DslParser/DslParser.cs` | Statement loop, `{ }` context stack, disambiguation, events `OnKeyword`, `OnMemberRead`, `OnUpdateContext` |
| `GenericParser` | `DslParser/GenericParser.cs` | Parses one concept's parameters from its member list |
| `DslSyntax`, `ConceptType`, `ConceptMemberSyntax` | `DslParser/DslSyntax.cs`, `ConceptType.cs`, `ConceptMemberSyntax.cs`, `ConceptMemberBase.cs` | Serializable grammar (no reference to concept assemblies) |
| `DslSyntaxFromPlugins`, `ConceptMembers`, `ConceptMember` | `src/Rhetos.Core/Dsl/*.cs` | Builds `DslSyntax` from `IConceptInfo` types by reflection |
| `DslSyntaxFile`, `DslDocumentationFile` | `DslParser/DslSyntaxFile.cs`, `DslDocumentationFile.cs` | Save/load `DslSyntax.json`, `DslDocumentation.json` in CacheFolder |
| `ExternalTextReader` | `src/Rhetos.Core/Dsl/ExternalTextReader.cs` | Resolves `<file>` references |
| `DiskDslScriptLoader` | `src/Rhetos.Core/Dsl/DiskDslScriptLoader.cs` | Script discovery |
| Errors and positions | `DslParser/DslSyntaxException.cs`, `src/Rhetos.Core.Integration/Utilities/FilePosition.cs`, `.../ScriptPositionReporting.cs` | Error codes, line/column, message formats |
| Tests | `test/Rhetos.Core.Test/Dsl/TokenizerTest.cs`, `DslParserTest.cs`, `GenericParserTest.cs` | Edge cases |

## 1. Tokenizer

### 1.1 Token rules [V]

The tokenizer skips whitespace. It then picks the token kind from the first character, checking in this order (`TokenizerInternals.GetNextToken_ValueType`):

| # | Starts with | Type | `Value` | Details |
|---|---|---|---|---|
| 1 | letter, digit or `_` (`char.IsLetterOrDigit`) | Text | the run of letters/digits/`_` | Unicode letters allowed (`čćšđž`), may start with a digit (`123abc`). Any other char (`.`, `-`, `$`) ends it |
| 2 | `"` or `'` | Text | content without quotes; a doubled quote becomes one quote | `'abc''def'` gives `abc'def`; the other quote char needs no escape; no backslash escapes; may span lines; unterminated gives RH0008 |
| 3 | `<` | Text | **the content of the referenced file**, not the path | Reads until `>`. A char from `Path.GetInvalidPathChars()` gives RH0010, EOF gives RH0009, an unresolved file gives RH0012 |
| 4 | `//` | Comment | text up to (excluding) `\r` or `\n` | `inline//comment` needs no space (`TokenizerTest.GetTokens_RemoveComments`) |
| 5 | `/*` | Comment | text up to the first `*/` | Not nested; `/***/` is valid; `/*/` is unclosed and gives RH0013 |
| 6 | any other char | Special | that single char | `;` `{` `}` `.` and any other symbol, each a separate token; a lone `/` too |

- Whitespace is exactly `' '`, `'\t'`, `'\n'`, `'\r'` (`TokenizerInternals.Whitespaces`) [V]. Other Unicode spaces (NBSP, form feed) become Special tokens and then cause a parse error [I].
- `Tokenizer.GetTokens` removes comments (`if (t.Type != TokenType.Comment)`) [V].
- To the parser, forms 1–3 look the same: type `Text` plus a value. The token does not record which form produced it [V]. To find out, a tool must read `Script[PositionInDslScript]` [I].
- A `<` always starts an external reference, so `<` never becomes a Special token [V].
- On .NET Core, `Path.GetInvalidPathChars()` returns `|`, `\0` and chars 1–31 on Windows, but only `\0` on Linux. A line break inside `<...>` therefore fails only on Windows [I, runtime behaviour].

### 1.2 External text resolution [V] (`ExternalTextReader.Read`)

`ExternalTextReader` returns the first candidate that exists:

| Order | Applies to | Candidate |
|---|---|---|
| 1 | `.sql` only | SQL resource key `{directory of DslScript.Name}/{reference}` with `/` separators, looked up with `ISqlResources.TryGet` |
| 2 | `.sql` only | `{dir}/{name}.{DatabaseLanguage}.sql` |
| 3 | `.sql` only | `{dir}/{name} ({DatabaseLanguage}).sql` |
| 4 | always | `{folder of DslScript.Path}/{reference}`; both `\` and `/` are accepted as separators |

The reader records files it has read in `ExternalFiles`. The build uses that list as additional inputs. `DatabaseLanguage` comes from `DslSyntax.DatabaseLanguage`.

### 1.3 Positions [V]

| Item | Rule | Source |
|---|---|---|
| `PositionInDslScript` | 0-based UTF-16 offset of the token's first char; includes the opening quote or `<` | `Tokenizer.cs` |
| `PositionEndInDslScript` | exclusive end offset | `Tokenizer.cs`, `TokenizerTest.GetTokens_Simple` (`Text:ab(0,2)`) |
| EOF token | begin = end = `Script.Length`, value `""`, one per script | `Tokenizer.cs` |
| Line | 1 + number of `\n` before the offset; a lone `\r` is not a line break | `ScriptPositionReporting.Line` |
| Column | 1-based count of UTF-16 units since the last `\n`; a tab counts as 1 | `ScriptPositionReporting.Column` |
| Script text | `File.ReadAllText(path, UTF8)`: BOM stripped, no newline normalization; falls back to the ANSI code page if U+FFFD appears | `src/Rhetos.Core.Integration/Utilities/FilesUtility.cs` `ReadAllText` |

## 2. Statement grammar

### 2.1 Shape

This grammar is my summary [I] of `DslParser.ExtractConcepts`, `DslParser.UpdateContextForNextConcept` and `GenericParser.ParseMembers`:

```
scripts   = { script }                       // one token list; each script ends with an EOF token
script    = { statement } EOF
statement = keyword { parameter } ( ";" | "{" { statement } "}" ) { "}" }
keyword   = Text                             // case-insensitive match with [ConceptKeyword]
parameter = Text                             // string member
          | keyPart { "." keyPart }          // reference member: the referenced concept's key members, recursively
          | (nothing)                        // parent member taken from the enclosing statement
```

| Topic | Rule [V] | Source |
|---|---|---|
| Keyword | Peeked with `ReadText`, so any Text token works, including a quoted one. Parsers are looked up in a `MultiDictionary` with `OrdinalIgnoreCase`, and `TryRead` also ignores case | `DslParser.ParseNextConcept`, `CreateGenericParsers`; `TokenReader.TryRead`; test `GenericParserTest.ParseKeywordCaseInsensitive` |
| Keyword source | `[ConceptKeyword]`, read with `inherit: false`. Concepts without a keyword (e.g. abstract `PropertyInfo`) cannot start a statement but can be referenced | `src/Rhetos.Core/Dsl/ConceptInfoHelper.cs` `GetKeyword`; `DslParser.CreateGenericParsers` |
| Parameter order | Public writable properties: base-class properties first, then declaration order (`MetadataToken`) | `src/Rhetos.Core/Dsl/ConceptMembers.cs`, `ConceptMember.cs` (`SortOrder1/2`) |
| Required | Every *parsable* member is required, key or not. There are no optional or named parameters | `GenericParser.ParseMembers`; `DslParserTest.DslParser_ErrorReporting` (`simple a` fails) |
| Non-parsable | Members returned by `IAlternativeInitializationConcept.DeclareNonparsableProperties()` are not read from the script | `ConceptMembers.cs`, `ConceptMember.IsParsable` |
| String member | Exactly one Text token | `GenericParser.ReadMemberValue` |
| Terminator | After the parameters, `{` pushes the statement onto the context stack; otherwise `;` is required (RH0001) | `DslParser.UpdateContextForNextConcept` |
| Closing | Any number of `}` is consumed, but only right after `;`, `{` or `}`. A `}` at root level gives RH0007 | same |
| Files | EOF tokens are skipped only at nesting depth 0, so a statement or block cannot span two files | `DslParser.ExtractConcepts`; `DslParserTest.DslParser_ConceptSplitOverFiles` |
| Key vs non-key | Key members identify the concept, and only key members are read when the concept is *referenced*. Non-key members are still required positional parameters of the concept's own statement | `GenericParser.ParseMembers` (`readingAReference ? IsKey : IsParsable`) |
| Derived concepts | Must not add `[ConceptKey]` members. They share the base key and add only non-key parameters, which makes them overloads with more parameters | `ConceptMembers.cs` |

### 2.2 Nesting and the parent parameter [V]

- **Parent property:** the last member with `[ConceptParent]`. Without one, the first member, if that member is a concept reference. Otherwise the concept has no parent property and can only be used at root level, like `Module` (`GenericParser.GetParentProperty`).
- **Context:** only the immediate enclosing statement (`context.Peek()`).
- **Direct match:** if the parent member's type is assignable from the enclosing concept's type, the parser takes the value from the context and reads no tokens for it (`ConceptType.IsInstanceOfType`). A nested statement therefore cannot also state its parent explicitly [I].
- **Partial match (implicit prefix):** if the type does not match directly, the parser goes into the parent member's referenced key members and tries the context there. Example from `GenericParserTest.ParseEnclosedPartiallyInline`: `enclosedlevel2 b c` nested in `simple a` gives Parent = `a.b`. Analogous CommonConcepts case [I]: `Module Demo { ShortString School Name; }`, because `PropertyInfo.DataStructure` (`DataStructureInfo` = Module + Name) gets its Module from the context. Source: `src/Rhetos.CommonConcepts/Dsl.DefaultConcepts/DataStructure/Properties/PropertyInfo.cs`.
- **No match:** `This concept cannot be nested within {Type}. Trying to read {Type}.`
- A member of type `IConceptInfo` (the interface) can only come from the context, so such concepts work only in nested syntax.
- A single-reference concept that references another single-reference concept must be nested. A recursive concept (parent of its own type) cannot be used at root level.

### 2.3 Reference parameters [V]

- **Dotted key:** a reference parameter reads the referenced concept's key members in order, recursively, with `.` between the string parts: `Demo.Region`, `Demo.School.Name` (`GenericParser`, flag `parsedFirstReferenceElement`). A missing dot gives `Parent property and the following key value (Name) must be separated with a dot. Expected "."` (`GenericParserTest.ParseEnclosedInlineError`).
- **Separate parameters:** whitespace separates them. Example: `Reference BelongsTo Demo.Region;` is Name `BelongsTo` plus the non-key reference `Referenced` = `Demo.Region` (`.../Properties/ReferencePropertyInfo.cs`).
- **Legacy dot:** a `.` before a key string member that is not the first member is still accepted, so the old flat form `ShortString Demo.School.Name;` parses. The parser records the warning `Obsolete syntax: Remove '.' from ShortString statement. At line …`.
  - The `ExcessDotInKey` option (`Rhetos:Build:DslSyntaxExcessDotInKey`, default `Ignore`, `src/Rhetos.Core.Integration/Utilities/BuildOptions.cs`) decides what happens to the warning:

    | Value | Effect |
    |---|---|
    | `Ignore` | trace log |
    | `Warning` | warning log |
    | `Error` | after the *whole* parse, `throw new DslSyntaxException(firstWarning)`, with no error code and no `FilePosition` (`DslParser.ExtractConcepts`) |

  - The wiki page `Rhetos-DSL-syntax.md` still shows this legacy dotted form. The current form is `ShortString Demo.School Name;` (`ChangeLog.md` 3.1.0).
- **Stub node:** a parsed reference is only a stub (`ConceptSyntaxNode`) that holds the key values. `src/Rhetos.Core/Dsl/DslContainer.cs` resolves it after macro expansion. If resolution fails, it throws `DslConceptSyntaxException` with `Referenced concept is not defined in DSL scripts: '…'`, and that exception has no file position.

### 2.4 Disambiguation of same-keyword concepts [V] (`DslParser.ParseNextConcept`, `Disambiguate`)

1. The parser runs every concept type registered for the keyword from the same start position, each with its own `TokenReader` copy.
2. An interpretation succeeds when all its members are read. The terminator is not checked at this step.
3. The parser keeps the interpretations that consumed the **most tokens**. A shorter overload wins only if no longer one succeeds.
4. Among the remaining ones, it prefers a concrete parent type over an `IConceptInfo` interface parent. Then it prefers the **lowest nesting depth**, i.e. the fewest parent hops ("flat over nested"). If exactly one interpretation is best, that one wins.
5. Otherwise the parser throws RH0006. If no interpretation succeeded, it throws RH0003, RH0004 or RH0005 (see §3.2).

The tests `DslParserTest.FlatNestedAmbiguity_*` and `ConcreteInterfaceAmbiguity_*` cover this. Parameter "types" affect parsing only in three ways: a string member takes one token, a reference member also consumes dots, and a parent member taken from the context consumes no tokens [I].

### 2.5 Edge cases found in code, not covered by tests [I]

| Input | Behaviour | Reason |
|---|---|---|
| `'Module' Demo;` | Parses as `Module` | The keyword peek uses `ReadText`, and `TryRead` compares only `Value` |
| `Module A '{' Entity B; '}'` | Quoted `{` `}` `;` `.` act as structure tokens | `TokenReader.TryRead` ignores `Token.Type` |
| `Module A {` then EOF | RH0005 `Invalid DSL script syntax.` at EOF, not RH0002 | Inside a block the EOF token is not skipped, so the keyword read fails |
| stray `}` at root | RH0007 is reported at the token *after* the `}` | `TryRead("}")` advances before the check |
| `};` or `;;` | RH0005 | `;` is not a Text token, so it cannot be a keyword |

With the standard `Tokenizer`, RH0002 (`Expected "}"`) happens only if the token list has no final EOF token. That can happen with a custom `ITokenizer`.

## 3. Error reporting

### 3.1 Exception types [V]

| Type | File | Position data | Raised for |
|---|---|---|---|
| `DslSyntaxException : RhetosException` | `DslParser/DslSyntaxException.cs` | `ErrorCode`, `FilePosition` (`Path`, `BeginLine`, `BeginColumn`, `EndLine`, `EndColumn`, all 1-based), `Details` | All tokenizer and parser errors in §3.2 |
| `DslSyntaxException(message)` without a code | `DslContainer.cs`, `DslModel.cs`, `DslParser.cs` | none | Same key described twice with different values, macro iteration limit, `ExcessDotInKey=Error` |
| `DslConceptSyntaxException : DslSyntaxException` | `src/Rhetos.Core/Dsl/DslConceptSyntaxException.cs` | none; the message starts with the concept's `GetUserDescription()` | Semantic errors after parsing: unresolved or circular references, uninitialized properties, concept validation |
| `FrameworkException` | e.g. `ConceptMembers.cs` | none | Invalid concept class definitions (plugin bugs) |

- `FilePosition` exists only if `DslScript.Path` is non-empty [V].
- The exception does not expose raw offsets. `DslSyntaxException.DslScript` and `.Position` were removed in 5.0 (`ChangeLog.md` 5.0.0) [V].
- The tokenizer returns tokens up to the error together with `SyntaxError` (`TokenizerResult.cs`). `DslParser` rethrows that error before it starts parsing [V].

### 3.2 Error codes [V]

| Code | Message (format) | Raised in | Reported range |
|---|---|---|---|
| RH0001 | `Expected ";" or "{".` | `UpdateContextForNextConcept` | The unexpected token after the parameters (EOF = zero-length at the end). Details list the previous concept and its property values |
| RH0002 | `Expected "}" to close concept "{node}".` | `ExtractConcepts` | Current token (see §2.5) |
| RH0003 | `Invalid parameters after keyword '{kw}'. Possible causes: {errors}` | `ParseNextConcept` | **Keyword token only.** Details: `Possible causes:` plus, per parser, `GenericParser: {error}` with position text, limited to 500 chars |
| RH0004 | `Unrecognized concept keyword '{kw}'.` | `ParseNextConcept` | Keyword token |
| RH0005 | `Invalid DSL script syntax.` | `ParseNextConcept` | Token where a keyword was expected (Special or EOF) |
| RH0006 | `Ambiguous syntax. There are multiple possible interpretations of keyword '{kw}': 1. {AssemblyQualifiedName}, 2. …` | `ParseNextConcept` | Keyword token |
| RH0007 | `Unexpected "}".` | `UpdateContextForNextConcept` | Token after the `}` |
| RH0008 | `Unexpected end of file within a quoted string. Missing closing character: {q}.` | `ReadQuotedString` | Opening quote to end of script |
| RH0009 | `Unexpected end of script within external text reference. Missing closing character: '>'.` | `ReadExternalText` | `<` to end of script |
| RH0010 | `Invalid filename character within external text reference.` | `ReadExternalText` | Begin = the invalid char. The code passes end = 0, so `FilePosition` End = (1,1) |
| RH0011 | Removed in 6.0 (was `Referenced empty file name … in DSL script.`) | git commit `d4867baf` | — |
| RH0012 | `Cannot find the file referenced in DSL script. File does not exist: '{path}'`, or `… None of the following exists:` plus a numbered candidate list | `ReadExternalText` / `ExternalTextReader` | The whole `<…>` |
| RH0013 | `Unexpected end of file within a block comment. Expected '*/'.` | `ReadBlockComment` | From after `/*` to end of script |

Typical inner errors inside RH0003 have the form `Cannot read the value of {Member} in {ConceptType}. {inner}` (`GenericParser.ParseMembers`). The inner part is one of:
- `Missing parameter at the end of the DSL script.`
- `Unexpected token type (Special ';') while reading text. Use quotes to specify text if that was intended.` (`TokenReader.ReadText`)
- the nesting and dot messages from §2.2 and §2.3.

### 3.3 Message formats [V]

| Form | Format | Source |
|---|---|---|
| `Message` | Short message from §3.2 | — |
| `FilePosition.CanonicalOrigin` | `{path}({bl},{bc},{el},{ec})`, or `{path}({bl},{bc})` when end ≤ begin | `FilePosition.cs` |
| `Details` | `Syntax error at "{≤70 chars before} >>>{≤70 chars after}"` (whitespace collapsed), plus any additional details | `DslSyntaxException.cs`, `ScriptPositionReporting.ReportPreviousAndFollowingTextInline` |
| `ToString()` / `MessageForLog()` | Message, newline, `CanonicalOrigin`, `Details:`, details indented by 3 | `DslSyntaxException.cs` |
| `rhetos build --msbuild-format` | `{CanonicalOrigin or "Rhetos DSL"}: error {ErrorCode}: {message with CR/LF replaced by spaces}` | `src/RhetosCli/Program.cs` `PrintCanonicalError` |

Examples from `DslParserTest.DslParser_ErrorReporting`:

| Script | Error | Origin | Range covers |
|---|---|---|---|
| `simple a` | RH0003 | `TestDslScript(1,1,1,7)` | the keyword |
| `simple a b` | RH0001 | `TestDslScript(1,11)` | the EOF token |

## 4. Script discovery [V]

| Aspect | Rule | Source |
|---|---|---|
| Extension | `.rhe`, case-insensitive | `DiskDslScriptLoader.TryGetDslScriptSubpath` |
| NuGet packages and referenced projects | Only files under `DslScripts\` in the package root | same |
| Current project | Any `**\*.rhe` (MSBuild item `RhetosBuild`). Since 5.3 no `DslScripts` folder is needed | `src/RhetosVSIntegration/MsBuildIntegrationBuild/Rhetos.MSBuild.targets`; `ChangeLog.md` 5.3.0 |
| `DslScript.Name` | `{PackageId}\{path under DslScripts\}`, or for the project `{ProjectName}\{project-relative path}` (a leading `DslScripts\` is removed); contains no version number | `DiskDslScriptLoader.LoadPackageScripts` |
| `DslScript.Path` | Physical path, used in error positions and for resolving `<file>` | same |
| Package order | Dependencies before dependents (topological sort of the NuGet lock-file graph, then reversed); the current project last. Subpackages from the `rhetos-build.settings.json` `Subpackages` option are inserted just before the project | `src/RhetosVSIntegration/NuGetUtilities.cs` `SortInstalledPackagesByDependencies`; `src/Rhetos.Core.Integration/Deployment/RhetosProjectContentProvider.cs` `SplitProjectToSubpackages` |
| Order within a package | `orderby file.InPackagePath` (default string comparer) | `DiskDslScriptLoader.LoadPackageScripts` |
| Package list | `obj\Rhetos\rhetos-project.assets.json`, written by the MSBuild task `ResolveRhetosProjectAssets` from NuGet `obj\project.assets.json`. The task touches the file on every build "to notify the language server" | `RhetosProjectContentProvider.cs`; `src/RhetosVSIntegration/ResolveRhetosProjectAssets.cs` |
| Grammar for tools | `DslSyntax.json` and `DslDocumentation.json` in CacheFolder (`obj\Rhetos`), rewritten only when the content changes | `DslSyntaxFile.cs`, `DslDocumentationFile.cs`, `src/Rhetos.Core/Dsl/DslSyntaxFileGenerator.cs` |

Statement and script order has no semantic meaning (wiki `Rhetos-DSL-syntax.md`, "Declarative programming"). Order only decides which error is reported first, because parsing stops at the first error [I].

## 5. Version differences

| Version | Change | Evidence |
|---|---|---|
| 3.1.0 / 4.0 | `.` only between parts of a reference; the legacy dot before a key string is tolerated (`ExcessDotInKey`); `[ConceptParent]` added | `ChangeLog.md` 3.1.0; `ExcessDotInKey.cs` comment ("Since Rhetos v4.0"); commit `0f3de344` (tag v3.1.0) |
| 4.0 | Error codes `RH0001…`, canonical `path(line,col)` origin, `--msbuild-format`; `DslParser` events for code-analysis tools | `ChangeLog.md` 4.0.0; commits `21387116`, `4be60442` (v4.0-beta) |
| 5.0 | `ITokenizer` / `TokenizerResult` (partial tokens on error); `DslSyntax` POCO and `DslSyntax.json`; `DslSyntax.Version` = 5.0; `DslSyntaxException.DslScript` / `.Position` removed | commits `571e54a5`, `1f05653e`, `609726dd`, `cf5d7f86` (all v5.0.0); `ChangeLog.md` 5.0.0 |
| 5.3 | `.rhe` files allowed anywhere in the current project | `ChangeLog.md` 5.3.0 |
| 5.4 | Block comments `/* */` (RH0013) | `ChangeLog.md` 5.4.0; commit `43d07f4c` |
| 6.0 | `Rhetos.Dsl.Interfaces.dll` + `Rhetos.Dsl.Parser.dll` merged into `Rhetos.Core.DslParser.dll` (namespace `Rhetos.Dsl` unchanged). Constructor changed from `Tokenizer(IDslScriptsProvider, FilesUtility, Lazy<DslSyntax>)` to `Tokenizer(IDslScriptsProvider, IExternalTextReader, Lazy<DslSyntax>)`. SQL resources allowed for `<x.sql>`. RH0011 removed. `DslSyntax.CurrentVersion` = 6.0. `DslSyntaxException(IConceptInfo, …)` replaced by `DslConceptSyntaxException` | `ChangeLog.md` 6.0.0; commits `1c61a2aa`, `d4867baf` |

`DslSyntaxFile.Load` logs a warning when `DslSyntax.json` has no `Version` (older than 5.0) or a version greater than `DslSyntax.CurrentVersion` [V].

## Relevant for Language Services

| Topic | Implication |
|---|---|
| Reuse | The tokenizer and parser run from `Rhetos.Core.DslParser` plus `DslSyntax.json`, without the concept assemblies (`ConceptType.cs` remarks) [V]. This repo already does this: LanguageServices `src/CodeAnalysis/Parsing/CodeAnalysisRun.cs` uses `Tokenizer`, re-runs `TokenizerInternals` to recover comment tokens, and subscribes to the `DslParser` events. `SimpleExternalResourceReader.cs` stubs `<file>` as the literal text `<path>` |
| Positions | Offsets are UTF-16, which matches the LSP default encoding. LSP line = `Line - 1`, character = `Column - 1`. Only `\n` breaks lines [I] |
| Inverted ranges | For RH0010, `FilePosition.End` is (1,1). Clamp any range whose end is before its begin. `CodeAnalysisRun.cs` currently maps End directly [I] |
| Null position | `FilePosition` is null when `DslScript.Path` is empty. Always give in-memory documents a non-empty `Path` [V] |
| Error granularity | RH0003, RH0004 and RH0006 cover only the keyword token. To locate the failing parameter, use `OnMemberRead`: it passes the `ITokenReader` (current position) and the member's `ValueOrError` [V] |
| No recovery | A tokenizer error in one script stops all later scripts, and the parser throws at its first error [V]. To report several errors, parse each document separately and add custom recovery, e.g. resynchronizing at the next `;` or `}` [I] |
| Per-document parsing | Nesting cannot cross files, so for *syntax* errors per-document parsing equals whole-project parsing [I]. Reference existence is checked only in the full model (`DslContainer`) and has no positions |
| Semantic tokens | `Token` does not say whether a Text token was an identifier, a quoted string or `<file>`; inspect the source char at `PositionInDslScript`. The value of a `<file>` token is the file content, not the path [V] |
| Keywords | Lookups must use `OrdinalIgnoreCase`. One keyword can map to several `ConceptType`s (overloads), so signature help should list all of them [V] |
| Obsolete dots | `DslSyntax.ExcessDotInKey` (from `DslSyntax.json`) tells whether legacy dots should be hidden, a warning, or an error. The warning text contains "At line X, column Y" but no structured position [V] |
| Version check | If `DslSyntax.Version` > 6.0, tell the user to update the extension (`DslSyntax.cs` remarks) [V] |
