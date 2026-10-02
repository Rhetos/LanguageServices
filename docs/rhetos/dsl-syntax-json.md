# DslSyntax.json: data model, generation and consumption (Rhetos 6)

`DslSyntax.json` is the serialized `Rhetos.Dsl.DslSyntax` object: the complete DSL grammar of one Rhetos application, including every concept (keyword) from every referenced plugin package. During `dotnet build`, the MSBuild target `BuildRhetosApp` runs `rhetos build` before C# compilation. The build creates the `DslSyntax` object in memory by reflection over all `IConceptInfo` plugin classes (`DslSyntaxFromPlugins`). The generator `DslSyntaxFileGenerator` then writes the object with Newtonsoft.Json to `<project>/obj/Rhetos/DslSyntax.json`. A second generator, `DslDocumentationFileGenerator`, writes `DslDocumentation.json` with concept summaries taken from XML doc comments. The Rhetos framework never reads either file. Its own `DslParser` uses the same in-memory `DslSyntax` object. The files exist so that external tools (Rhetos Language Services) can parse DSL scripts without loading the application's plugin assemblies. Only the build writes the file; the application runtime and `rhetos dbupdate` neither write nor read it.

Paths are relative to the Rhetos repo root (github.com/Rhetos/Rhetos, see README Sources), unless prefixed with `LanguageServices:` (this repository), `Compat:` (github.com/Rhetos/LanguageServicesCompatibility) or `Bookstore/` (github.com/Rhetos/Bookstore, branch `master`, see README Sources). Legend: **V** = verified in code, a real generated file, or git history. **I** = inference, not directly verified.

## 1. Quick facts

| Item | Value | V/I | Source |
|---|---|---|---|
| File name | `DslSyntax.json` (`DslSyntaxFile.DslSyntaxFileName`; the same constant is duplicated in `DslSyntaxFileGenerator`) | V | `src/Rhetos.Core.DslParser/DslSyntaxFile.cs`, `src/Rhetos.Core/Dsl/DslSyntaxFileGenerator.cs` |
| Folder | `RhetosBuildEnvironment.CacheFolder` = `<ProjectDir>/$(BaseIntermediateOutputPath)Rhetos`, normally `obj/Rhetos/` | V | `src/RhetosVSIntegration/ResolveRhetosProjectAssets.cs`, `src/RhetosVSIntegration/MsBuildIntegrationBuild/Rhetos.MSBuild.targets` |
| Model classes | `DslSyntax`, `ConceptType`, `ConceptMemberSyntax : ConceptMemberBase`, enum `ExcessDotInKey` | V | `src/Rhetos.Core.DslParser/`, `src/Rhetos.Core.Integration/Utilities/ExcessDotInKey.cs` |
| NuGet package with model and reader | `Rhetos.DslParser`: `Rhetos.Core.DslParser.dll` (netstandard2.1) and `Rhetos.Core.Integration.dll`; depends on Newtonsoft.Json 13.0.3 | V | `src/Rhetos.DslParser.nuspec` |
| Writer | `DslSyntaxFileGenerator : IGenerator` calls `DslSyntaxFile.Save` | V | `src/Rhetos.Core/Dsl/DslSyntaxFileGenerator.cs` |
| Reader | `DslSyntaxFile.Load()`. Nothing in Rhetos `src/` calls it. Callers are tests and `LanguageServices:src/CodeAnalysis/Tools/DslSyntaxProvider.cs` | V | grep; `test/Rhetos.Core.Test/Dsl/DslSyntaxFileTest.cs` |
| DSL syntax version | `DslSyntax.CurrentVersion = new Version(6, 0)` | V | `src/Rhetos.Core.DslParser/DslSyntax.cs` |
| DI registration | Only in `BuildModule` (the build-time container); not in `RuntimeModule` or `DbUpdateModule` | V | `src/Rhetos.Core/Configuration.Autofac/Modules/BuildModule.cs` |
| Wiki coverage | None. No wiki page (Rhetos-wiki repo) mentions `DslSyntax` or `DslDocumentation` | V | grep |

## 2. Data model

### 2.1 `DslSyntax` (root object): `src/Rhetos.Core.DslParser/DslSyntax.cs`

| JSON property | C# type → JSON type | Example (real file) | Meaning and source of the value |
|---|---|---|---|
| `$id` | Newtonsoft metadata | `"1"` | Reference id (§3) |
| `Version` | `System.Version` → string | `"6.0"` | DSL syntax version of the app. `DslSyntaxFromPlugins.CreateDslSyntax` sets it to `DslSyntax.CurrentVersion` |
| `RhetosVersion` | `string` | `"6.1.1-dev26091419311c635d+1c635dc…"` | Rhetos framework version in SemVer 2.0, "information only" (`SystemUtility.GetRhetosVersion()`) |
| `ExcessDotInKey` | enum → integer | `0` | Parser option. Source: build setting `Rhetos:Build:DslSyntaxExcessDotInKey` (`BuildOptions`, default `Ignore`); §6.3 |
| `DatabaseLanguage` | `string` | `"MsSql"` | Source: build setting `Rhetos:Build:DatabaseLanguage` (`DatabaseSettings`, default `"MsSql"`; `Rhetos.PostgreSql` expects `"PostgreSql"`) |
| `ConceptTypes` | `List<ConceptType>` | 272 items | All concept types, sorted by `AssemblyQualifiedName` |

The doc comments on `ExcessDotInKey` and `DatabaseLanguage` say that these values are persisted "to be available to the external language server" (V, `DslSyntax.cs`). Build settings are read from `rhetos-build.settings.json` in the project folder (V, `src/RhetosCli/Program.cs` `Build`).

### 2.2 `ConceptType`: `src/Rhetos.Core.DslParser/ConceptType.cs`

| JSON property | C# type | Meaning |
|---|---|---|
| `AssemblyQualifiedName` | `string` | `Type.AssemblyQualifiedName` of the `IConceptInfo` class. This is the concept's identity. It contains the assembly version, for example `Rhetos.Dsl.DefaultConcepts.ModuleInfo, Rhetos.CommonConcepts, Version=6.1.1.0, Culture=neutral, PublicKeyToken=null` |
| `BaseTypes` | `List<ConceptType>` | Base classes that implement `IConceptInfo`, excluding the class itself, ordered from the root to the direct parent class. An empty list if there are none. The elements are object references |
| `TypeName` | `string` | Short class name, for example `ModuleInfo` |
| `Keyword` | `string` or `null` | Value of `[ConceptKeyword]`. It is `null` for base or internal concepts that cannot be written in DSL (`ConceptInfoHelper.GetKeyword`, `src/Rhetos.Core/Dsl/ConceptInfoHelper.cs`) |
| `Members` | `List<ConceptMemberSyntax>` | Concept parameters in parse order (§2.3) |

The following are methods and are not serialized: `GetRootTypeName()` (the `TypeName` of the first `BaseTypes` element, or the type's own name), `GetKeywordOrTypeName()`, `IsInstanceOfType(node)` and `IsAssignableFrom(type)` (V).

### 2.3 `ConceptMemberSyntax`: `ConceptMemberSyntax.cs` and `ConceptMemberBase.cs`

The values are computed by `src/Rhetos.Core/Dsl/ConceptMember.cs` and `src/Rhetos.Core/Dsl/ConceptMembers.cs`. In JSON, `ConceptType` comes first, followed by the base-class properties (V, real file).

| JSON property | Type | Meaning (how it is computed) |
|---|---|---|
| `ConceptType` | `ConceptType` or `null` | Referenced concept type, usually written as `$ref`. It is set only when `IsConceptInfo && !IsConceptInfoInterface` |
| `Index` | `int` | Position in `Members` after sorting; the index into `ConceptSyntaxNode.Parameters` |
| `Name` | `string` | C# property name |
| `IsConceptInfo` | `bool` | The property type is assignable to `IConceptInfo`, which means the member references another concept |
| `IsKey` | `bool` | The property has `[ConceptKey]`. Key members form the concept's unique key. When another statement references this concept, the parser reads only the key members |
| `IsParentNested` | `bool` | The property has `[ConceptParent]`, which overrides the default parent property. In the Rhetos repo, only tests use this attribute; the sample file has 0 such members |
| `SortOrder1` | `int` | Negative inheritance depth of the declaring class, so properties declared in base classes sort first |
| `SortOrder2` | `int` | `MemberInfo.MetadataToken`, which gives the declaration order within a class |
| `IsDerived` | `bool` | The property is declared in a base class (`DeclaringType != ReflectedType`) |
| `IsStringType` | `bool` | The property type is `string` |
| `IsConceptInfoInterface` | `bool` | The property type is exactly `IConceptInfo`. It accepts any concept, but only through nested syntax |
| `IsParsable` | `bool` | `false` for the names returned by `IAlternativeInitializationConcept.DeclareNonparsableProperties()`. The parser skips these members, and the concept initializes them later (`src/Rhetos.Core/Dsl/IAlternativeInitializationConcept.cs`). The sample has 94 such members |

Member rules from `ConceptMembers.Create` (V): only public instance properties with a setter are members, and a public field throws an exception. Each concept needs at least one `IsKey` member. A derived concept must not declare new key members.

No `ConceptMemberType` enum exists in the Rhetos 6 sources (V, grep). `IDslSyntax` was replaced by the POCO `DslSyntax` in 2021, and there is no `DslSyntaxLoader`. The boolean flags determine the member kind:

| Flags | Member kind | DSL form accepted by `GenericParser` (`src/Rhetos.Core.DslParser/GenericParser.cs`) |
|---|---|---|
| `IsStringType` | String value | One text token: an identifier, a quoted string, or an external text reference `<file>` |
| `IsConceptInfo && !IsConceptInfoInterface` | Reference to the concrete concept `ConceptType` | The key values of the referenced concept separated by `.`, or the enclosing `{ }` parent |
| `IsConceptInfoInterface` | Reference to any concept | Only the enclosing parent statement (nested form) |
| Other types | Not supported | The parser returns the error "GenericParser does not support members of type …" |

### 2.4 `ExcessDotInKey` enum: `src/Rhetos.Core.Integration/Utilities/ExcessDotInKey.cs`

| Name | JSON value | Effect in `DslParser.ExtractConcepts` |
|---|---|---|
| `Ignore` | 0 (default) | "Obsolete syntax" warnings are logged at Trace level |
| `Warning` | 1 | The warnings are logged as warnings |
| `Error` | 2 | The parser throws the first warning as a `DslSyntaxException` |

A source comment states that the numeric values "are important for backward compatibility of serialized options in DslSyntax class".

## 3. Serialization details (`DslSyntaxFile`)

| Aspect | Value | V/I, source |
|---|---|---|
| Library | Newtonsoft.Json 13.0.3: `JsonConvert.SerializeObject` for writing, `JsonSerializer.Create(...).Deserialize<DslSyntax>` for reading | V, `DslSyntaxFile.cs`, `src/Rhetos.Core.Integration/Rhetos.Core.Integration.csproj` |
| Settings | `PreserveReferencesHandling.Objects`, `ReferenceLoopHandling.Serialize`, `Formatting.Indented`. No `TypeNameHandling`, no converters. Null values are written | V, `DslSyntaxFile.cs` |
| References | Every object gets `"$id"`. A `ConceptType` instance that occurs again is written as `{"$ref": "N"}`. Lists have no `$id` | V, real file |
| First occurrence | Each `ConceptType` is written in full at its first occurrence in document order. That occurrence can be deep inside another concept's `BaseTypes` or a member's `ConceptType`. As a result, entries of the top-level `ConceptTypes` list can be `$ref` (57 of 272 in the sample) | V, real file |
| Enums | Integer | V, `"ExcessDotInKey": 0` |
| `Version` | String `"6.0"` | V, real file. The Rhetos 4 compatibility package adds `VersionConverter` because Rhetos 4 uses Newtonsoft 12 (`Compat:src/Rhetos.LanguageServicesCompatibility/Rhetos.Dsl.Parser/DslSyntaxFile.cs`) |
| Encoding | `File.WriteAllText(path, text)` writes UTF-8 without BOM | V, code; the real file starts with the bytes `{\r\n` |
| Line endings | CRLF in the sample built on Windows. On Linux they are probably LF | V (sample); I (other OS) |
| Write only if changed | `Save` compares the new text with the existing file and does not write when they are equal. The code comment says this "is useful for optimization of external DSL analysis, such as DSL IntelliSense plugin" | V, `DslSyntaxFile.cs` |
| Deterministic order | `ConceptTypes` are sorted by `AssemblyQualifiedName` (commit `73d6dfab`, "Sorting DslSyntax.json to reduce reloads of LSP"). `Members` are sorted by `SortOrder1`, then `SortOrder2` | V, `src/Rhetos.Core/Dsl/DslSyntaxFromPlugins.cs`, git |
| Sample size | 417,737 bytes, 13,621 lines, 272 concept types (231 with a keyword, 41 without), 936 `$id`, 505 `$ref`. 271 types come from `Rhetos.CommonConcepts` and one (`InitializationConcept`) from `Rhetos.Core`. A released app, the v6 Bookstore (Rhetos 6.0.0): 422,323 bytes, 13,783 lines, 276 concept types (235 with a keyword): 271 from `Rhetos.CommonConcepts`, 1 from `Rhetos.Core`, 4 from its concept project `Bookstore.RhetosExtensions`; 947 `$id`, 513 `$ref` (58 in the top-level list); `RhetosVersion` `"6.0.0+fb2442579d2e…"` (the commit of tag `v6.0.0`) | V, `test/CommonConcepts.TestApp.MsSql/obj/Rhetos/DslSyntax.json`, `Bookstore/src/Bookstore.Service/obj/Rhetos/DslSyntax.json` |

Consequence for readers: deserialize the file with Newtonsoft and `PreserveReferencesHandling`, as `DslSyntaxFile.Load` does. Any other JSON reader must resolve the `$id`/`$ref` pairs itself.

Real excerpt, trimmed (`…` marks removed parts), from `test/CommonConcepts.TestApp.MsSql/obj/Rhetos/DslSyntax.json`:

```jsonc
{
  "$id": "1",
  "Version": "6.0",
  "RhetosVersion": "6.1.1-dev26091419311c635d+1c635dc97872f66dca7afce64a51d2d0c616a8da",
  "ExcessDotInKey": 0,
  "DatabaseLanguage": "MsSql",
  "ConceptTypes": [
    // … "$id": "5" (ModuleInfo) is written in full inside the first entry, ActionInfo.BaseTypes[0].Members[0].ConceptType
    {
      "$id": "795",
      "AssemblyQualifiedName": "Rhetos.Dsl.DefaultConcepts.SqlDependsOnModuleInfo, Rhetos.CommonConcepts, Version=6.1.1.0, Culture=neutral, PublicKeyToken=null",
      "BaseTypes": [],
      "TypeName": "SqlDependsOnModuleInfo",
      "Keyword": "SqlDependsOn",
      "Members": [
        {
          "$id": "796",
          "ConceptType": null,
          "Index": 0,
          "Name": "Dependent",
          "IsConceptInfo": true,
          "IsKey": true,
          "IsParentNested": false,
          "SortOrder1": 0,
          "SortOrder2": 385876322,
          "IsDerived": false,
          "IsStringType": false,
          "IsConceptInfoInterface": true,
          "IsParsable": true
        },
        {
          "$id": "797",
          "ConceptType": { "$ref": "5" },
          "Index": 1,
          "Name": "DependsOn",
          "IsConceptInfo": true,
          "IsKey": true,
          // … same flags as above, except "SortOrder2": 385876323, "IsConceptInfoInterface": false
        }
      ]
    },
    // …
  ]
}
```

## 4. Companion file `DslDocumentation.json`

| Item | Value | V/I, source |
|---|---|---|
| Model | `DslDocumentation { Dictionary<string, ConceptDocumentation> Concepts }`. The key is the concept's `AssemblyQualifiedName`. `ConceptDocumentation { string Summary; string Remarks }` | V, `src/Rhetos.Core.DslParser/DslDocumentation.cs`, `ConceptDocumentation.cs` |
| File and folder | `DslDocumentation.json` in the same `CacheFolder` (`obj/Rhetos/`) | V, `DslDocumentationFile.cs` |
| Settings | `Formatting.Indented`, `NullValueHandling.Ignore`. No reference handling, so no `$id`. Written only if the content changed | V, `DslDocumentationFile.cs` |
| Generator | `DslDocumentationFileGenerator`, priority 2, runs right after `DslSyntaxFileGenerator` | V, `src/Rhetos.Core/Dsl/DslDocumentationFileGenerator.cs`, `src/Rhetos.Core/Deployment/ApplicationGenerator.cs` |
| Input | For each `ConceptType`, the generator calls `Type.GetType(AssemblyQualifiedName)` and loads `<assembly folder>/<assembly name>.xml` (next to `Assembly.Location`). It then reads `<member name="T:{Type.FullName}">` and takes `<summary>` and `<remarks>` | V, same file |
| Text processing | All text nodes and attribute values are concatenated. Each attribute value is reduced to its last dot segment, so `<see cref="T:…EntityInfo"/>` becomes `EntityInfo`. Lines are trimmed and joined with single spaces, so paragraphs and line breaks are lost. Concepts that have neither a summary nor remarks are omitted | V, same file |
| Errors | Failures while loading or reading XML are logged as warnings and do not fail the build | V, same file |
| Availability of XML files | Rhetos sets `GenerateDocumentationFile=true` (`Directory.Build.props`). `src/Rhetos.CommonConcepts.nuspec` ships `Rhetos.CommonConcepts.xml` in `lib/net10.0`. Third-party plugins have documentation only if their packages ship the `.xml` file | V; I (third-party) |
| Not included | Documentation of members (properties); only `T:` entries are read | V |
| Nested classes | `Type.FullName` uses `+` for nested classes, but XML doc IDs use `.`. A concept declared as a nested class would therefore get no documentation | I |
| Sample | 250 `Summary` and 10 `Remarks` entries for 272 concept types (`test/CommonConcepts.TestApp.MsSql/obj/Rhetos/DslDocumentation.json`). v6 Bookstore: also 250 `Summary` and 10 `Remarks` entries, for 276 concept types; its 4 own concepts have no entry because `Bookstore.RhetosExtensions` does not generate an XML documentation file (`Bookstore/src/Bookstore.Service/obj/Rhetos/DslDocumentation.json`) | V |

```json
"Rhetos.Dsl.DefaultConcepts.AllPropertiesFromInfo, Rhetos.CommonConcepts, Version=6.1.1.0, Culture=neutral, PublicKeyToken=null": {
  "Summary": "Copies all properties from another data structure, along with the associated Required, SqlIndex and Extends concepts.",
  "Remarks": "It will not copy the Extends concept (UniqueReference) if the source is an extension of the destination."
},
```

## 5. Generation during `dotnet build`

| # | Step | What happens | Source |
|---|---|---|---|
| 1 | MSBuild target `ResolveRhetosBuildItems` (`BeforeTargets="ResolveRhetosProjectAssets;CollectUpToDateCheckInputDesignTime"`) | Collects `**\*.rhe`, `DataMigration` and `Resources` as `RhetosBuild` items, and `rhetos-build.settings.json` as a `RhetosBuildSettings` item | `Rhetos.MSBuild.targets` |
| 2 | Target `ResolveRhetosProjectAssets` (`DependsOnTargets` ResolveReferences, `BeforeTargets="BeforeCompile"`) | The task writes `obj/Rhetos/rhetos-project.assets.json`. The file contains the `RhetosBuildEnvironment` (including `CacheFolder`) and the plugin assemblies from NuGet packages and referenced projects. The task writes the file only if the content changed, but it always updates the timestamp, "to notify the language server" | `src/RhetosVSIntegration/ResolveRhetosProjectAssets.cs`, `src/Rhetos.Core.Integration/Deployment/RhetosProjectContentProvider.cs` |
| 3 | Target `BuildRhetosApp` (`BeforeTargets="BeforeCompile"`; condition `RhetosBuild=True`, not `DesignTimeBuild`, `BuildingProject`; `Inputs="@(RhetosInput)" Outputs="@(RhetosOutput)"`) | Deletes `Rhetos.BuildComplete`, runs `dotnet <package>/tools/rhetos.dll --msbuild-format build <ProjectDir>`, and writes `Rhetos.BuildComplete` again when the command succeeds | `Rhetos.MSBuild.targets`, `Rhetos.MSBuild.props` |
| 4 | `rhetos build` → `Program.Build` → `ApplicationBuild.GenerateApplication` | Loads `rhetos-project.assets.json` and `rhetos-build.settings.json`, scans plugins, builds the Autofac container with `BuildModule`, and calls `ApplicationGenerator.ExecuteGenerators()` | `src/RhetosCli/Program.cs`, `src/Rhetos.Core/Configuration.Autofac/ApplicationBuild.cs` |
| 5 | `ApplicationGenerator.DslParserPriority` | Orders the generators: `DslSyntaxFileGenerator` = 1, `DslDocumentationFileGenerator` = 2, all others = 3. A `ParallelJob` then runs them | `src/Rhetos.Core/Deployment/ApplicationGenerator.cs` |
| 6 | `DslSyntaxFileGenerator.Generate` | Saves the singleton `DslSyntax`. `DslSyntaxFromPlugins.CreateDslSyntax` creates the singleton from all `[Export(typeof(IConceptInfo))]` plugins, plus `InitializationConcept` (registered manually), plus all `IConceptInfo` base classes (abstract classes are not resolvable from DI) | `src/Rhetos.Core/Dsl/DslSyntaxFromPlugins.cs`, `BuildModule.cs` |
| 7 | Target `AddRhetosSourceFiles`, then `CoreCompile` | Adds `obj/Rhetos/Source/**/*.cs` to the `Compile` items, so the C# compiler runs after the file is written | `Rhetos.MSBuild.targets` |

The syntax files are generated first so that "DSL IntelliSense will work even if there are syntax errors in DSL script or if any other IGenerator fails" (V, comment in `ApplicationGenerator.cs`; introduced by commit `a28d982b`, 2021-12-17). This works for two reasons. First, creating `DslSyntax` does not parse any `.rhe` file, because `DslModel` parses lazily when another generator first uses it (V, `src/Rhetos.Core/Dsl/DslModel.cs`). Second, `ParallelJob.RunAllTasks` lets tasks that are already running finish before it rethrows an error (V, `src/Rhetos.Core/Utilities/ParallelJob.cs`). The file is probably not written when `CreateDslSyntax` itself throws, for example because a concept class has no `[ConceptKey]` or has a public field. In that case, resolving the generator plugins fails before any generator runs (I).

| Incremental-build situation | Effect on `DslSyntax.json` | V/I |
|---|---|---|
| No `RhetosInput` (`.rhe` files, settings, `project.assets.json`, `@(ReferencePath)` DLLs, `Rhetos.BuildItems`, external source items) is newer than `Rhetos.BuildComplete` | MSBuild skips `BuildRhetosApp`, and the file is not touched | V |
| Only `.rhe` files changed | `rhetos build` runs. The serialized content is identical, so `Save` does not rewrite the file and its timestamp stays the same | V (code) |
| A plugin package or a referenced project changed | The content changes (assembly versions in `AssemblyQualifiedName`, metadata tokens in `SortOrder2`, `RhetosVersion`), so the file is rewritten | I |
| The previous `rhetos build` failed | `Rhetos.BuildComplete` is missing, so the next build runs `BuildRhetosApp` again | V |
| Visual Studio design-time build | `BuildRhetosApp` is skipped. `ResolveRhetosProjectAssets` has no such condition | V |
| Clean | No Rhetos target deletes `obj/Rhetos/DslSyntax.json`. `Rhetos.Host.targets` `AfterClean` deletes only `EfMappingGeneratedViews.json`. Whether `dotnet clean` removes the file was not verified | V / unknown |
| Runtime and `rhetos dbupdate` | Not used. `DslSyntax` is registered only in `BuildModule`. The file is not in `obj/Rhetos/Assets`, so it is not copied to `bin` | V |

Other files in `obj/Rhetos/` of a built app (V, sample folder listing): `rhetos-project.assets.json`, `DslSyntax.json`, `DslDocumentation.json`, `MacroOrder.json`, `PluginScanner.json`, `Rhetos.BuildItems`, `Rhetos.BuildComplete`, `Rhetos.ExternalSourceItems`, `RhetosGeneratedSourceFiles.txt`, `Rhetos.DatabaseUpdated`, `Source/`, `Assets/`, `LinqPad/`.

## 6. Consumption by the framework

### 6.1 The framework parses with the in-memory model, not with the file

Commit `4bd1cbc0` (2021-05-07) changed the parser to work "with abstract syntax instead of IConceptInfo instances, to allow serialization of DSL grammar". Reflection over the plugin classes now happens once, in `DslSyntaxFromPlugins`. `Rhetos.Core.DslParser.csproj` references only `Rhetos.Core.Integration`; it does not reference `Rhetos.Core` or any plugin assembly (V).

| Consumer | What it uses | Source |
|---|---|---|
| `DslParser` (receives `Lazy<DslSyntax>`) | Creates one `GenericParser` for each `ConceptType` whose `Keyword` is not null, in a multi-dictionary keyed by keyword with `OrdinalIgnoreCase`. Uses `ExcessDotInKey` for the warnings | `src/Rhetos.Core.DslParser/DslParser.cs` |
| `GenericParser` | `Keyword` and `Members`: `IsParsable`, `IsKey`, `IsStringType`, `IsConceptInfo`, `IsConceptInfoInterface`, `IsParentNested`, `ConceptType` | `GenericParser.cs` |
| `DslParser.Disambiguate` | Follows parent-property chains through `ConceptType` to choose between several successful interpretations | `DslParser.cs` |
| `ExternalTextReader` | `DatabaseLanguage`: for `<x.sql>` it tries `x.<DatabaseLanguage>.sql`, then `x (<DatabaseLanguage>).sql`, then `x.sql` | `src/Rhetos.Core/Dsl/ExternalTextReader.cs` |
| `Tokenizer` / `TokenizerInternals` | Receive `DslSyntax`. `TokenizerInternals` stores the object but does not use it | `src/Rhetos.Core.DslParser/TokenizerInternals.cs` (grep) |
| `ConceptInfoHelper.ConvertNodesToConceptInfos` | After parsing, creates the real `IConceptInfo` objects with `Type.GetType(AssemblyQualifiedName)`, `Activator.CreateInstance` and `ConceptMembers` reflection | `src/Rhetos.Core/Dsl/ConceptInfoHelper.cs` |

Parsing rules that depend only on the model (V, `DslParser.cs`, `GenericParser.cs`):
- The candidate concepts are all `ConceptType`s whose keyword equals the first token, compared case-insensitively.
- The parser reads the `IsParsable` members in `Index` order.
- The parent property is the last member with `IsParentNested`. If there is none, it is the first member, if that member `IsConceptInfo`.
- Inside a `{ }` block, the parent property takes the enclosing concept when `ConceptType.IsInstanceOfType` returns true, or always when the member is the `IConceptInfo` interface.
- A referenced concept is read by its `IsKey` members only, separated by `.`.
- A statement ends with `;` or `{`.
- When several interpretations succeed, the parser keeps the one that consumes the most tokens, then the one with the flattest nesting. If more than one remains, it reports error RH0006. The other error codes are RH0001–RH0007.

### 6.2 Version check

`DslSyntaxFile.Load` never throws because of the version. It only logs a warning:
- when `Version` is null: "Cannot detect the application's DSL syntax version";
- when `Version > DslSyntax.CurrentVersion`: "The project uses a newer version of the DSL syntax".

The remarks on `DslSyntax.Version` describe the contract. Tools such as DSL IntelliSense "should report a warning or an error" when the application's version is larger than the version of the libraries they use, and "it is expected that the new version will support previous DSL versions" (V, `DslSyntax.cs`; tests in `test/Rhetos.Core.Test/Dsl/DslSyntaxFileTest.cs`). Language Services turns both cases into an initialization error (V, `LanguageServices:src/CodeAnalysis/Services/RhetosProjectContext.cs`).

### 6.3 `ExcessDotInKey` semantics

The doc comment says: "Before Rhetos v4.0, dot character was expected before string key parameter of current statement. Since Rhetos v4.0, dot should only be used for separating key parameters of referenced concept." The check is in `GenericParser.ReadMemberValue`. When the parser reads the statement's own string key member that is not the first member, and the next token is `.`, it consumes the dot and adds the warning "Obsolete syntax: Remove '.' from {Keyword} statement." Example derived from the code: `ShortString` has the members `DataStructure` (reference) and `Name` (string). The current flat syntax is `ShortString Bookstore.Book Title;`, and the legacy syntax `ShortString Bookstore.Book.Title;` produces the warning. The parser accepts the legacy dot in all three modes; the mode only selects the log level, or an error after parsing. The option can also change which interpretation wins in ambiguous cases (V, comments in `test/Rhetos.Core.Test/Dsl/DslParserTest.cs`).

## 7. History and backward compatibility

| Date | Commit | Change | First release tag |
|---|---|---|---|
| 2021-05-07 | `4bd1cbc0` | The parser works with the abstract `ConceptType` syntax instead of `IConceptInfo` | v5.0.0 |
| 2021-05-10 | `609726dd` | `DslSyntaxFileGenerator` added; `DslSyntax.json` introduced | v5.0.0 |
| 2021-05-17 | `1f05653e`, `9dc67e5e` | `IDslSyntax` replaced by the POCO `DslSyntax` with Newtonsoft. Lists replaced arrays to fix "Cannot preserve reference to array" | v5.0.0 |
| 2021-06-14 | `867dbe14`, `73d6dfab` | `DslDocumentationFileGenerator` added; `DslSyntax.json` sorted | v5.0.0 |
| 2021-06-17 | `d85ad4a7` | `BaseTypesAssemblyQualifiedName` (strings) replaced by `BaseTypes` (object references). `RootTypeName` and `KeywordOrTypeName` are no longer serialized | v5.0.0 |
| 2021-12-14 | `cf5d7f86` | `Version` became a `System.Version` holding the DSL syntax version `5.0`. Before this commit, `Version` held the Rhetos SemVer string; that value moved to the new `RhetosVersion` | v5.0.0 |
| 2023-10-23 | `1c61a2aa`, `b1415ca2` | `Rhetos.Dsl.Interfaces.dll` and `Rhetos.Dsl.Parser.dll` merged and renamed to `Rhetos.Core.DslParser.dll`. The NuGet id `Rhetos.DslParser` and the namespaces did not change | v6.0.0 |
| 2023-12-04 | `d4867baf` | `CurrentVersion` changed from 5.0 to 6.0. External file references in DSL can now reference SQL resources. In this commit the JSON structure did not change; only the constant changed | v6.0.0 (2025-09-02) |

(V: `git log`, `git tag --contains`; release dates from `ChangeLog.md`: 5.0.0 = 2022-03-25, 6.0.0 = 2025-09-02.)

- Only two `CurrentVersion` values ever existed: 5.0 and 6.0 (V, `git log -G`). Rhetos 5.4 added block comments to the tokenizer (LanguageServices `CHANGELOG.md` 2.2.0) without a version change. Therefore `Version` does not change for every syntax change.
- **Rhetos 4 and older** do not generate the file. `Rhetos.LanguageServicesCompatibility` 1.0.0 (2022-03-25) is a DSL plugin package for Rhetos v4 apps. It registers its own copies of `DslSyntaxFromPlugins`, `DslSyntaxFileGenerator` and `DslDocumentationFileGenerator` (`Compat:src/Rhetos.LanguageServicesCompatibility/AutofacModuleConfiguration.cs`) and writes both files to `obj\Rhetos` with `CurrentVersion = 5.0`. The source comment says: "Rhetos.LanguageServicesCompatibility on Rhetos v4 generates DSL syntax files compatible with v5.0". `ConceptMemberAdapter` converts the Rhetos 4 `ConceptMember`. Rhetos v5 and newer need no plugin (V, `Compat:Readme.md`, `LanguageServices:README.md`).
- Test data in LanguageServices: `LanguageServices:test/RhetosMockAppContents/DslSyntax.json` has `Version` `"5.0"` and was produced by Rhetos `5.0.0-dev220309…`. `…/RhetosAppFolder/FolderWithAppUnsupported/MockObj/Rhetos/DslSyntax.json` has `Version` `"1111.0"` and tests the unsupported-version path (V).

## 8. Relevant for Language Services

The file is the ideal editor input for these reasons:
- It is the exact grammar that the build-time parser uses, including the app's custom plugin concepts.
- It is plain JSON in a fixed location, readable by the same `DslSyntaxFile.Load` that Rhetos uses (package `Rhetos.DslParser` 6.0.0, `LanguageServices:src/CodeAnalysis/CodeAnalysis.csproj`). The editor does not need to load the app's DLLs.
- It is written early, even when the DSL scripts contain errors or another generator fails.
- It is rewritten only when its content changes, so the file timestamp is a cheap reload signal. `RhetosProjectContext` compares `DslSyntaxProvider.GetLastModifiedTime()`.

| The editor can get | From |
|---|---|
| All keywords and their overloads (several `ConceptType`s per keyword) | `Keyword` |
| Parameter names, order, kind (string, concrete reference, any concept) and which parameters are skipped | `Members`, the flags in §2.3 |
| The format of a reference (dot-separated keys of the referenced concept) | `IsKey`, `ConceptType.Members` |
| Which concept can be nested in which parent | Parent-property rule, `ConceptType`, `BaseTypes` (`IsAssignableFrom`) |
| Hover text | `DslDocumentation.json` `Summary` and `Remarks` |
| Lookup of SQL-dialect-specific external files; the legacy-dot policy; the compatibility check | `DatabaseLanguage`, `ExcessDotInKey`, `Version` |

| The editor cannot get from these files | Notes |
|---|---|
| Concept instances (entity names and similar) from other scripts or packages | The file contains types only. Value completion must come from parsing the scripts |
| Semantic validation, macro expansion, `IValidatedConcept` checks | Only `rhetos build` does these (I, `DslModel`) |
| The C# type or purpose of string members (identifier, C# snippet, SQL) | Only `IsStringType` is stored |
| Documentation of parameters | Only type-level `T:` docs are stored |
| Tokenizer rules (comments, quoting, `<file>`) | These are in code (`Tokenizer`), so the editor's tokenizer matches the `Rhetos.DslParser` package version it references, not the app's version |
| Changes made after the last successful plugin scan | The file is stale until the next `dotnet build` (skipped in design-time builds) |
| A stable concept id across package versions | `AssemblyQualifiedName` contains the assembly version, so match by `TypeName` or by namespace and type name across builds |

Until 2026-10-02 the LanguageServices README said that detection uses `obj/Rhetos/rhetos-project.assets.json` and that the app's `.dll` files are loaded; both statements predated `DslSyntax.json` and were corrected in the README. The code checks `obj/Rhetos/DslSyntax.json` (`DslSyntaxProvider.IsValidProjectRootPath`) and `LanguageServices:src` contains no `Assembly.Load*` calls (V).
