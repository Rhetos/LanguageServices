# Rhetos DSL concepts and plugins

A Rhetos DSL keyword is a C# class that implements the empty marker interface `IConceptInfo` and carries the attributes `[Export(typeof(IConceptInfo))]` and `[ConceptKeyword("...")]`. The DSL parameters of a keyword are the public settable properties of that class, in a fixed order: base-class properties first, then declaration order. Each property is a `string` or a reference to another concept. `[ConceptKey]` marks the properties that form the unique key. `[ConceptParent]` (or, by default, a first property that is a concept reference) marks the property that the enclosing concept fills when the statement is nested inside `{ }`. At build time, `rhetos build` scans every runtime DLL of every NuGet package and every referenced project for `[Export]` attributes. It registers the concept classes in Autofac and serializes their syntax to `obj/Rhetos/DslSyntax.json`. It also copies the classes' XML `<summary>`/`<remarks>` into `obj/Rhetos/DslDocumentation.json`. Both files exist so that an external tool such as this language server can parse DSL without loading the plugin assemblies. Other plugin interfaces (macros, code generators, database generators, validation, alternative initialization) attach behaviour to concept types. Only `IAlternativeInitializationConcept` changes the parse-time syntax.

Legend: **V** = verified in code, git history or a generated file (path cited). **I** = inference from code reading, not tested. Paths are relative to the Rhetos repo root (github.com/Rhetos/Rhetos, 6.1.1-dev, commit `1c635dc9`, see README Sources) unless prefixed with `Wiki/`, `LS:` (this repo), `Bookstore/` or `Bookstore v4/` (github.com/Rhetos/Bookstore at branch `master` and at its Rhetos 4 revision; see README Sources).

## 1. `IConceptInfo` and the metadata attributes

| Item | Rule | V/I | Source |
|---|---|---|---|
| `IConceptInfo` | Empty interface, namespace `Rhetos.Dsl`, no XML doc. Compiled into `Rhetos.Core.dll`. | V | `src/Rhetos.Core/Dsl/IConceptInfo.cs` |
| Attribute assembly | `ConceptKeyword`, `ConceptKey`, `ConceptParent`, `ConceptType`, `DslSyntax`, `DslParser`, `GenericParser` are in `Rhetos.Core.DslParser.dll` (NuGet `Rhetos.DslParser`, netstandard2.1; its Readme says it is meant for Rhetos apps and IDE plugins). | V | `src/Rhetos.DslParser.nuspec`, `src/Rhetos.Core.DslParser/Readme.md` |
| `[ConceptKeyword("X")]` | Class attribute. It is the only source of the DSL keyword. It is read with `GetCustomAttributes(..., inherit: false)`, so **a derived class does not inherit its base's keyword** (even though `AttributeUsage` says `Inherited = true`). | V | `src/Rhetos.Core.DslParser/ConceptKeywordAttribute.cs`, `src/Rhetos.Core/Dsl/ConceptInfoHelper.cs` `GetKeyword` |
| Missing keyword | `Keyword = null`. The parser creates parsers only for types with a non-null keyword, so the class cannot be written in DSL ("abstract concept", e.g. `PropertyInfo`). **No name derivation**: the `Info` suffix is not stripped. The class name appears only in messages (`GetKeywordOrTypeName`). The `<Keyword>Info` class naming is a wiki convention, not a rule. | V | `src/Rhetos.Core.DslParser/DslParser.cs` `CreateGenericParsers` (`Where(c => c.Keyword != null)`), `Wiki/Rhetos-concept-development.md` |
| Keyword matching | Case-insensitive (`StringComparer.OrdinalIgnoreCase`). Many classes may share one keyword (overloads). | V | `src/Rhetos.Core.DslParser/DslParser.cs` |
| `[ConceptKey]` | Property attribute. At least one key member is required (`FrameworkException`). The key string is `<root type name> <key values joined by '.'>`, e.g. `DataStructureInfo Bookstore.Book`. An `IConceptInfo`-typed key value gets a `RootType:` prefix. | V | `src/Rhetos.Core/Dsl/ConceptMembers.cs`, `ConceptInfoHelper.cs` `GetKey`/`AppendMember` |
| `[ConceptParent]` | Property attribute. It is allowed only on a property whose type is an `IConceptInfo` (otherwise `FrameworkException`). It is needed for recursive concepts. Added in Rhetos 3.1. | V | `src/Rhetos.Core/Dsl/ConceptMember.cs`, `src/Rhetos.Core.DslParser/ConceptParentAttribute.cs`, `ChangeLog.md` 3.1.0 |
| `[Export(typeof(IConceptInfo))]` | `System.ComponentModel.Composition.ExportAttribute`. Only the attribute is used: the scanner reads it with reflection, and Autofac is the container (no MEF container). | V | `src/Rhetos.Core/Extensibility/PluginScanner.cs` `GetMefExportsForTypes` |
| `[Obsolete("msg")]` on the class | Build logs the warning `Obsolete concept <keyword> ... (N occurrences). msg`. This flag is **not** written to `DslSyntax.json`. | V | `src/Rhetos.Core/Dsl/DslModel.cs` `ReportObsoleteConcepts`, `src/Rhetos.Core.DslParser/ConceptType.cs` |
| Other attributes | None found. There is no attribute for optional members, defaults or member docs. | V (grep) | `src/Rhetos.Core.DslParser/*.cs` |

### 1.1 Members (`ConceptMembers.Get`)

| Rule | V/I | Source |
|---|---|---|
| Members are the **public instance properties with a setter** (`CanWrite`). Get-only properties such as `DataStructureInfo.FullName => ...` are not members. Public fields throw `FrameworkException`. At least one member is required. | V | `src/Rhetos.Core/Dsl/ConceptMembers.cs` |
| Order: `SortOrder1 = -inheritance depth` (members of the most-base class come first), then `SortOrder2 = MetadataToken` (source declaration order). `Index` is assigned after sorting. `ConceptSyntaxNode.Parameters[Index]` uses this order. | V | `src/Rhetos.Core/Dsl/ConceptMember.cs`, `GenericParser.cs` class remarks ("order as the properties are declared") |
| Supported types: `string` (`IsStringType`), exactly `IConceptInfo` (`IsConceptInfoInterface`), or a concept class (`IsConceptInfo`, `ConceptMemberSyntax.ConceptType` set). The parser and the key builder reject any other type, such as int, bool or an interface derived from `IConceptInfo`. | V (code paths) / I (no test seen) | `src/Rhetos.Core.DslParser/GenericParser.cs` `ReadMemberValue`, `ConceptInfoHelper.cs` `AppendMember`, `src/Rhetos.Core/Dsl/DslSyntaxFromPlugins.cs` |
| A referenced concept class must be in the syntax: it must be exported or be a base class of an exported concept. Otherwise `DslSyntaxFromPlugins does not contain concept type ...`. | V | `src/Rhetos.Core/Dsl/DslSyntaxFromPlugins.cs` |
| `IsParsable = false` for the names that `IAlternativeInitializationConcept.DeclareNonparsableProperties()` returns (see 4.1). | V | `src/Rhetos.Core/Dsl/ConceptMember.cs` |

### 1.2 How the parser uses members (`GenericParser`)

| Situation | Behaviour | Source (V) |
|---|---|---|
| Member list | Statement: members with `IsParsable`. Reference to another concept: members with `IsKey` only (non-parsable keys included). | `GenericParser.ParseMembers` |
| Parent member | `GetParentProperty`: the **last** member with `[ConceptParent]`, else the first member if it is a concept reference, else none (the concept is root-only; nesting it gives "cannot be nested within"). | `GenericParser.GetParentProperty` |
| Nested statement | The parent member is taken from the immediately enclosing concept (`context.Peek()`) if its declared `ConceptType.IsAssignableFrom` the enclosing type (same type or derived). Otherwise the parent member is parsed as a reference, and the enclosing concept may fill that reference's own parent (recursive), e.g. a property-level concept nested in an `Entity`. | `ReadMemberValue`, `ConceptType.IsInstanceOfType` |
| `IConceptInfo`-typed member | It accepts any enclosing concept. It is usable **only nested** (error "Member of type IConceptInfo can only be nested ..."). | `ReadMemberValue`, `test/Rhetos.Core.Test/Dsl/GenericParserTest.cs` `InterfaceReference_*` |
| Reference member (flat) | A sub-parser for the **declared** member type reads that type's key members, dot-separated, e.g. `Bookstore.Book`. The result is a stub of the declared type. `DslContainer` later replaces the stub by key (root type name + key values), so a `DataStructureInfo` reference resolves to an `EntityInfo`, `SqlQueryableInfo`, etc. | `ReadMemberValue`, `src/Rhetos.Core/Dsl/DslContainer.cs` `ReplaceReferencesWithFullConcepts`, `ConceptInfoHelper.ConvertNodeToConceptInfo` |
| String member | `tokenReader.ReadText()`: an identifier, a quoted string, or an external-file token. | `ReadMemberValue` |
| Dot before a string key | Since 3.1 a dot is required only inside reference keys: `Entity Bookstore Book;`. The legacy form `Entity Bookstore.Book;` still parses but produces the warning "Obsolete syntax: Remove '.'". That warning goes to the trace log, the warning log, or becomes an error, per `ExcessDotInKey` (default `Ignore`; the value is saved in `DslSyntax.json`). | `ReadMemberValue`, `src/Rhetos.Core.Integration/Utilities/BuildOptions.cs`, `ChangeLog.md` 3.1.0 |
| Same keyword, several classes | All parsers for the keyword run. The parser keeps the interpretation that consumed the most tokens. If several remain, it prefers a concrete parent type over an `IConceptInfo` parent, then the lowest nesting depth. If still more than one remains: error RH0006 "Ambiguous syntax". Other errors: RH0001 `;`/`{` expected, RH0002 missing `}`, RH0003 invalid parameters, RH0004 unknown keyword, RH0005 invalid syntax. | `src/Rhetos.Core.DslParser/DslParser.cs` `ParseNextConcept`, `Disambiguate` |
| Special errors | A recursive concept cannot be root. A single-reference concept whose parent is also single-reference must be nested. | `ReadMemberValue` |

## 2. Real examples

`src/Rhetos.CommonConcepts/Dsl.DefaultConcepts/DataStructure/DataStructureInfo.cs` and `EntityInfo.cs` (inheritance; key members are declared only in the root type):

```csharp
[Export(typeof(IConceptInfo))]
[ConceptKeyword("DataStructure")]
public class DataStructureInfo : IValidatedConcept
{
    [ConceptKey]
    public ModuleInfo Module { get; set; }

    [ConceptKey]
    public string Name { get; set; }

    public string FullName => Module.Name + "." + Name;   // get-only: not a DSL member
    ...
}

[Export(typeof(IConceptInfo))]
[ConceptKeyword("Entity")]
public class EntityInfo : DataStructureInfo, IWritableOrmDataStructure
{
}
```

`.../DataStructure/Properties/ReferencePropertyInfo.cs` and `.../SimpleBusinessLogic/SimpleReferencePropertyInfo.cs` (overloaded keyword; non-key member added in a derived class; alternative initialization):

```csharp
[Export(typeof(IConceptInfo))]
[ConceptKeyword("Reference")]
public class ReferencePropertyInfo : PropertyInfo          // PropertyInfo: [ConceptKey] DataStructure, [ConceptKey] Name, no keyword
{
    public DataStructureInfo Referenced { get; set; }
    ...
}

[Export(typeof(IConceptInfo))]
[ConceptKeyword("Reference")]
public class SimpleReferencePropertyInfo : ReferencePropertyInfo, IAlternativeInitializationConcept
{
    public IEnumerable<string> DeclareNonparsableProperties() { return new[] { "Referenced" }; }

    public void InitializeNonparsableProperties(out IEnumerable<IConceptInfo> createdConcepts)
    {
        Referenced = new DataStructureInfo { Module = DataStructure.Module, Name = Name };
        createdConcepts = null;
    }
}
```

`.../DatabaseWorkarounds/SqlDependsOnDataStructureInfo.cs` (an `IConceptInfo`-typed member; a macro plugin bound by its generic argument):

```csharp
[Export(typeof(IConceptInfo))]
[ConceptKeyword("SqlDependsOn")]
public class SqlDependsOnDataStructureInfo : IConceptInfo
{
    [ConceptKey]
    public IConceptInfo Dependent { get; set; }

    [ConceptKey]
    public DataStructureInfo DependsOn { get; set; }
}

[Export(typeof(IConceptMacro))]
public class SqlDependsOnDataStructureMacro : IConceptMacro<SqlDependsOnDataStructureInfo>
{
    public IEnumerable<IConceptInfo> CreateNewConcepts(SqlDependsOnDataStructureInfo conceptInfo, IDslModel existingConcepts) { ... }
}
```

| Concept | Ordered members (`*` key, `P` parent, `x` non-parsable) | Resulting DSL forms | V/I |
|---|---|---|---|
| `EntityInfo` | `Module*P`, `Name*` | `Module Bookstore { Entity Book; }`, `Entity Bookstore Book;` | V (members in `LS: test/RhetosTestApp/obj/Rhetos/DslSyntax.json`); forms I |
| `ReferencePropertyInfo` | `DataStructure*P`, `Name*`, `Referenced` | `Entity Book { Reference Author Bookstore.Person; }` | members V, form I |
| `SimpleReferencePropertyInfo` | `DataStructure*P`, `Name*`, `Referenced x` | `Entity Book { Reference Person; }` (same module) | members V, form I |
| `SqlDependsOnDataStructureInfo` | `Dependent*P` (IConceptInfo), `DependsOn*` | nested only: `SqlQueryable X ... { SqlDependsOn Bookstore.Book; }` | I |

## 3. Derived (inherited) concepts

| Aspect | Behaviour | V/I | Source |
|---|---|---|---|
| Keyword | Not inherited. A derived class needs its own `[ConceptKeyword]`. Without it, the class is not parsable (test class `ExtendedRootMenuCI`). | V | `ConceptInfoHelper.GetKeyword`, `test/Rhetos.Core.Test/Dsl/GenericParserTest.cs` |
| Export | Each class needs its own `[Export]`: the scanner reads `type.GetCustomAttributesData()`, which returns only attributes declared on that type. | V | `src/Rhetos.Core/Extensibility/PluginScanner.cs` |
| Members | Base members are included and ordered first. A derived class **must not declare new `[ConceptKey]` members** (`FrameworkException`). It may add non-key members (e.g. `SqlQueryableInfo.SqlSource`, `ReferencePropertyInfo.Referenced`). | V | `src/Rhetos.Core/Dsl/ConceptMembers.cs` |
| Key / identity | The key uses the root class name. `Entity Bookstore.X` and `SqlQueryable Bookstore.X` therefore collide ("Concept with same key is described twice with different values"). A duplicate key is accepted only if the new concept's type is the same as, or a base of, the existing concept's type and the new concept's non-key values are equal; the new concept is then ignored. | V | `ConceptInfoHelper.GetKey`, `src/Rhetos.Core/Dsl/DslContainer.cs` `ConteptsValueEqualOrBase` |
| Parent override | A derived class may add a `[ConceptParent]` member. The last one wins, which allows recursive concepts (test `SubMenuCI : MenuCI`). | V | `ConceptParentAttribute.cs` remarks, `GenericParserTest.cs` |
| Syntax file | `ConceptType.BaseTypes` lists the base concept classes root-first. Base classes are added even if they are not exported, because abstract classes cannot be resolved from DI. `GetRootTypeName()` = `BaseTypes[0]`. | V | `src/Rhetos.Core/Dsl/DslSyntaxFromPlugins.cs`, `src/Rhetos.Core.DslParser/ConceptType.cs` |
| Plugins for a base type | Code generators and other `GetImplementations` plugins run for derived concepts (`CsUtility.GetClassHierarchy`). `IConceptMacro<T>` is evaluated with `ImplementsDerivations = true`. `ConceptMetadata` walks `BaseType`. | V | `src/Rhetos.Core/Extensibility/PluginsContainer.cs`, `src/Rhetos.Core/Dsl/DslModel.cs`, `ConceptMetadata.cs` |
| Abstract / generic classes | CommonConcepts has no abstract or generic concept class (grep). Autofac constructs one instance of each exported concept class during discovery, and `Activator.CreateInstance` creates one instance per parsed node. An abstract class with a keyword would therefore get a parser but fail at node conversion. Generic concept classes are not supported anywhere visible. | V (grep) / I | `ConceptInfoHelper.ConvertNodeToConceptInfo` |

## 4. Other concept-related plugin interfaces

| Interface | Implemented by | Binding to concept type | Effect | Source (V) |
|---|---|---|---|---|
| `IMacroConcept` | the concept class | itself | `CreateNewConcepts()` (no parameters since v5) returns new concepts; a duplicate is ignored. | `src/Rhetos.Core/Dsl/IMacroConcept.cs`, `ChangeLog.md` 5.0.0 #10 |
| `IConceptMacro<T>` | separate class, `[Export(typeof(IConceptMacro))]` | generic argument `T` | `CreateNewConcepts(T, IDslModel)`. Throws "Macro ... is not registered properly" if `T` is not an exported concept. | `IConceptMacro.cs`, `Configuration.Autofac/Modules/BuildModule.cs`, `DslModel.ListMacroEvaluators` |
| `IValidatedConcept` | the concept class | itself | `CheckSemantics(IDslModel)` runs after macro expansion. It replaced `IValidationConcept`, which was removed in v5. | `IValidatedConcept.cs`, `DslModel.CheckSemantics`, `ChangeLog.md` |
| `IAlternativeInitializationConcept` | the concept class | itself | Removes members from the parsed syntax; see 4.1. | `IAlternativeInitializationConcept.cs` |
| `IConceptCodeGenerator` | separate class | `[ExportMetadata(MefProvider.Implements, typeof(X))]` | `GenerateCode(IConceptInfo, ICodeBuilder)` (C#). | `src/Rhetos.Core/Compiler.Interfaces/IConceptCodeGenerator.cs`, `Wiki/Rhetos-concept-development.md` |
| `IConceptDatabaseGenerator<T>` | separate class | generic argument | SQL generation, new in v6. `IConceptDatabaseDefinition(+Extension)` is `[Obsolete]` but still registered. | `src/Rhetos.Core/DatabaseGenerator.Interfaces/*.cs`, `BuildModule.cs` |
| `IConceptDataMigration<T>`, `ConceptMapping<T>`, `IConceptMetadataExtension<T>` | separate class | generic argument | Data-migration SQL, ORM mapping, and per-concept metadata (`ConceptMetadata.Get`). | `BuildModule.cs`, `src/Rhetos.Core/Dsl/ConceptMetadata.cs` |
| `MefProvider.DependsOn` metadata | any plugin | | Orders plugins topologically. | `src/Rhetos.Core/Extensibility.Interfaces/MefProvider.cs`, `PluginScanner.SortByDependency` |

Build order (V, `src/Rhetos.Core/Dsl/DslModel.cs` `Initialize`): parse, then convert nodes to `IConceptInfo`, then `InitializeNonparsableProperties`, then add to `DslContainer` and resolve references by key, then expand macros (at most 200 iterations), then `CheckSemantics`, then write the model to `DslModel.json`.

### 4.1 `IAlternativeInitializationConcept` and parsing

| Rule | V/I | Source |
|---|---|---|
| `DeclareNonparsableProperties()` names members (base-class members allowed) that the DSL statement does not take. Those members get `IsParsable = false` in `DslSyntax.json`, but they stay in `Members`, keep their `Index`, and can still be key members. | V | `ConceptMembers.cs`, `ConceptMember.cs` |
| The method is called once per type on a **new instance** (`Activator.CreateInstance`), so it must return a constant list and the class needs a parameterless constructor. A name that is not a member throws `FrameworkException`. | V | `ConceptMembers.Create` |
| `InitializeNonparsableProperties(out createdConcepts)` runs after parsing, before reference resolution. Referenced concepts are still key-only stubs at that point, except the parent when nested. It may create concepts (e.g. `DateRangeInfo` creates two `DatePropertyInfo`). | V | `IAlternativeInitializationConcept.cs` remarks, `AlternativeInitialization.cs`, `.../SimpleBusinessLogic/DateRangeInfo.cs` |
| A reference **to** such a concept from another statement uses its key members, which may be the non-parsable ones. Example: `SqlIndexInfo` parses only `Property`, but its key is `DataStructure` + `PropertyNames`. | V (rule) / I (example impact) | `GenericParser.ParseMembers`, `.../DatabaseWorkarounds/SqlIndexInfo.cs` |
| A macro may create such a concept without setting the non-parsable members. | V | `IAlternativeInitializationConcept.cs`, `DslModel.ExpandMacroConcepts` |

## 5. Discovery: from NuGet package to registered concept

| Step | What happens | V/I | Source |
|---|---|---|---|
| 1. MSBuild target `ResolveRhetosProjectAssets` (package `Rhetos.MSBuild`) | Reads `obj/project.assets.json`. Writes `obj/Rhetos/rhetos-project.assets.json` = `{ RhetosBuildEnvironment, RhetosProjectAssets { InstalledPackages, Assemblies } }`. `Assemblies` = every runtime `.dll` of every package library for the single target framework, plus the outputs of referenced projects. The file is touched on every build "to notify the language server", even when unchanged. | V | `src/RhetosVSIntegration/ResolveRhetosProjectAssets.cs`, `NuGetUtilities.cs`, `MsBuildIntegrationBuild/Rhetos.MSBuild.targets` |
| 2. Plugin package | **Any** NuGet package or referenced project. There is no marker, tag or naming rule. Wiki convention: `lib\` for DLLs, `DslScripts\` for `.rhe`, `DataMigration\`, `Resources\`. In packages and referenced projects, DSL scripts must be under `DslScripts\`; in the app project any `**\*.rhe` counts. | V | `NuGetUtilities.GetInstalledPackages`, `src/Rhetos.Core/Dsl/DiskDslScriptLoader.cs`, `Wiki/Creating-a-Rhetos-package.md` |
| 3. `rhetos build` | Loads the assets file and constructs `PluginScanner(projectAssets.Assemblies, ...)`. | V | `src/RhetosCli/Program.cs` `Build`, `src/Rhetos.Core/Configuration.Autofac/ApplicationBuild.cs` |
| 4. Scan | Skips files that match `PluginScannerOptions` (predefined: `Rhetos.Core.dll`, `Rhetos.Core.DslParser.dll`, `Microsoft.*`, `System.*`, `Autofac.*`, ...; extend with `Rhetos:PluginScanner:IgnoreAssemblyFiles`). Loads each assembly and collects `[Export(typeof(T))]` and `[ExportMetadata]`, grouped by `T.FullName`. Results are cached in `obj/Rhetos/PluginScanner.json`, keyed by path and `LastWriteTimeUtc`. | V | `PluginScanner.cs`, `PluginScannerOptions.cs`, `PluginScannerCache.cs` |
| 5. Register | `BuildModule.AddDsl`: `FindAndRegisterPlugins<IConceptInfo>()`. `InitializationConcept` (no keyword) is registered manually because core DLLs are not scanned. | V | `src/Rhetos.Core/Configuration.Autofac/Modules/BuildModule.cs` |
| 6. Syntax files | `DslSyntaxFromPlugins` resolves `IEnumerable<IConceptInfo>` and builds `DslSyntax`. `DslSyntaxFileGenerator` and `DslDocumentationFileGenerator` run **first among all generators**, "to make sure that DSL IntelliSense will work even if there are syntax errors in DSL script". A file is rewritten only when its content changes. | V | `DslSyntaxFromPlugins.cs`, `src/Rhetos.Core/Deployment/ApplicationGenerator.cs` `DslParserPriority`, `src/Rhetos.Core.DslParser/DslSyntaxFile.cs` |

## 6. XML documentation

| Fact | V/I | Source |
|---|---|---|
| All Rhetos projects set `GenerateDocumentationFile=true`. The NuGet packages ship `Rhetos.Core.xml`, `Rhetos.CommonConcepts.xml` and `Rhetos.Core.DslParser.xml` next to the DLLs in `lib\`. | V | `Directory.Build.props`, `src/Rhetos.nuspec`, `src/Rhetos.CommonConcepts.nuspec`, `src/Rhetos.DslParser.nuspec` |
| `DslDocumentationFileGenerator` loads `<AssemblyName>.xml` from the folder of `Assembly.Location` for each concept type in `DslSyntax`. It reads only the `T:<FullName>` member's `<summary>` and `<remarks>`. It flattens whitespace, replaces each attribute value (e.g. `cref`) with its last `.` segment, and skips empty entries. It writes `obj/Rhetos/DslDocumentation.json` = `{ "Concepts": { "<AssemblyQualifiedName>": { "Summary", "Remarks" } } }`. | V | `src/Rhetos.Core/Dsl/DslDocumentationFileGenerator.cs`, `src/Rhetos.Core.DslParser/DslDocumentation.cs`, `ConceptDocumentation.cs` |
| Not exported: property docs (`P:`), `<param>`, `<example>`, and the `[Obsolete]` message. Nothing else in Rhetos reads the XML files. | V (grep) | same |
| Rhetos 4.0: "Added summaries to all DSL concepts in CommonConcepts. They are displayed in Rhetos DSL IntelliSense for Visual Studio." | V | `ChangeLog.md` 4.0.0 |
| A custom concept project gets hover docs only if it generates its `.xml` next to the DLL that the build loads. The test concept `WithParentInfo` has no summary and so has no entry. The Bookstore shows both cases: the v4 concept project sets `<DocumentationFile>`, and `DeactivateOnDeleteInfo` has an entry; the v6 `Bookstore.RhetosExtensions` has `<summary>` comments on `CodeTableInfo` and `DeactivateOnDeleteInfo` but does not set `GenerateDocumentationFile`, so none of its 4 concepts has an entry. | V | `LS: test/RhetosTestAppConcepts/WithParentInfo.cs`, `LS: test/RhetosTestApp/obj/Rhetos/DslDocumentation.json`, `Bookstore v4/src/Bookstore.Concepts/Bookstore.Concepts.csproj`, `Bookstore/src/Bookstore.RhetosExtensions/`, `Bookstore/src/Bookstore.Service/obj/Rhetos/DslDocumentation.json` |

## 7. Version differences (4 / 5 / 6)

| Area | Rhetos 4 | Rhetos 5 | Rhetos 6 | Source (V) |
|---|---|---|---|---|
| Assemblies | `Rhetos.Dsl.Interfaces.dll` (IConceptInfo, attributes), `Rhetos.Dsl.DefaultConcepts.dll` | same layout, plus `Rhetos.Dsl.Parser` | `Rhetos.Core.dll`, `Rhetos.Core.DslParser.dll`, `Rhetos.CommonConcepts.dll`; namespaces unchanged | `git ls-tree v4.3.0 / v5.6.0`, `ChangeLog.md` 6.0.0 |
| Concept `AssemblyQualifiedName` (key in both JSON files) | only with `Rhetos.LanguageServicesCompatibility`: `Rhetos.Dsl.DefaultConcepts.ActionInfo, Rhetos.Dsl.DefaultConcepts, Version=4.3.0.0` | `Rhetos.Dsl.DefaultConcepts.ActionInfo, Rhetos.Dsl.DefaultConcepts, Version=5.0.0.0` | `..., Rhetos.CommonConcepts, Version=6.0.0.0` | `Bookstore v4/src/Bookstore.Service/obj/Rhetos/DslSyntax.json`, `LS: test/CodeAnalysis.Test/bin/.../DslSyntax.json`, `LS: test/RhetosTestApp/obj/Rhetos/DslDocumentation.json` |
| `DslSyntax.json` / `DslDocumentation.json` | absent (no `DslSyntax.cs` in v4.3.0) | introduced; `Version` 5.0 | `Version` 6.0 (bumped in commit `d4867baf`, "DSL scripts can reference SQL resources") | `git log`/`git tag --contains 4bd1cbc0, 867dbe14` (first tag v5.0.0) |
| Parser input | `IConceptInfo` instances | abstract `ConceptType` syntax, "to allow ... DSL IntelliSense plugins" in an external process | same | commit `4bd1cbc0` |
| `IMacroConcept.CreateNewConcepts` | `(IEnumerable<IConceptInfo> existingConcepts)` | `()` | `()` | `git show v4.3.0:Source/Rhetos.Dsl.Interfaces/IMacroConcept.cs`, `ChangeLog.md` 5.0.0 |
| `IValidationConcept` | obsolete | removed | removed | `ChangeLog.md` |
| DB generator interface | `IConceptDatabaseDefinition` | same | `IConceptDatabaseGenerator<T>`; old one obsolete | `ChangeLog.md` 6.0.0 |
| Build / target framework | Rhetos CLI + `rhetos-project.assets.json` new (DeployPackages still supported), .NET Framework | .NET 5; DeployPackages removed | .NET 8 (6.1.1-dev: .NET 10) | `git grep v4.0.0`, `ChangeLog.md` |
| Unchanged since ≤4 | keyword lookup `inherit:false`; `ConceptParent` (3.1); dot rule (3.1) | | | `git show v4.3.0:.../ConceptInfoHelper.cs` |

Wiki notes: `Wiki/Rhetos-concept-development.md` still names `Rhetos.Dsl.Interfaces` / `Rhetos.Compiler.Interfaces` as the defining assemblies, and `Wiki/Creating-a-Rhetos-package.md` shows a `net472` / Rhetos 2.0 nuspec. Both are pre-v6 and outdated for Rhetos 6. `Wiki/Backward-compatible-feature-implementation...md` and `Wiki/Low-level-object-model-concepts.md` contain nothing about concept definition beyond DI registration.

## 8. Relevant for Language Services

| Editor need | Metadata | Where it comes from |
|---|---|---|
| Keyword completion | `ConceptType.Keyword` (skip null), case-insensitive, several types per keyword | `obj/Rhetos/DslSyntax.json` |
| Signature help / parameter list | `Members` filtered by `IsParsable`, in `Index` order; type from `IsStringType` / `IsConceptInfoInterface` / `ConceptType`; key flag `IsKey`. When nested, skip the parent member chosen by `GenericParser.GetParentProperty` (which also uses `IsParentNested`). | `DslSyntax.json`; reuse `Rhetos.DslParser` (`LS: src/CodeAnalysis/CodeAnalysis.csproj` references 6.0.0) |
| Valid nesting / overloads | `ConceptType.IsAssignableFrom` (uses `BaseTypes`); disambiguation rules from §1.2 | `Rhetos.DslParser` |
| Reference value completion | Key members of the member's declared `ConceptType`, dot-separated. Candidates: parsed statements whose type is assignable to it. Macro-created and alternative-initialized concepts do not exist at parse time (I); they exist only in `obj/Rhetos/Assets/DslModel.json` after a build. | `DslSyntax.json`, `DslModel.cs` |
| Hover | Type-level `Summary` / `Remarks` only, keyed by `AssemblyQualifiedName` (the key format differs between v5 and v6). There are no member docs. | `obj/Rhetos/DslDocumentation.json` |
| Deprecation hints | Not available: `[Obsolete]` is not serialized. | n/a |
| Project detection and refresh | `obj/Rhetos/DslSyntax.json` (the current LS detection rule); `obj/Rhetos/rhetos-project.assets.json` is touched on every build. `DslSyntax.json` is rewritten only on change, and is generated even when the DSL has errors. | `ResolveRhetosProjectAssets.cs`, `ApplicationGenerator.cs`, `LS: src/CodeAnalysis/Tools/DslSyntaxProvider.cs` |
| Version check | `DslSyntax.Version` vs `DslSyntax.CurrentVersion`. `DslSyntaxFile.Load` logs a warning if the app's version is newer or missing. | `src/Rhetos.Core.DslParser/DslSyntax.cs`, `DslSyntaxFile.cs` |
| Legacy dot syntax | Honour `DslSyntax.ExcessDotInKey` (0 Ignore, 1 Warning, 2 Error) when reporting "Obsolete syntax" | `DslSyntax.json`, `ExcessDotInKey.cs` |
