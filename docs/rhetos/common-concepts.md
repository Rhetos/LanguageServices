# Rhetos.CommonConcepts: standard DSL concepts

`Rhetos.CommonConcepts` is the standard DSL library of Rhetos 6. It defines the keywords that almost every `.rhe` script uses: `Module`, `Entity`, property types, `Reference`, validations, filters, `Action`, `SqlQueryable`, `Persisted`, `RowPermissions`, SQL objects, and others. Each keyword is one C# class (`*Info.cs`) with `[ConceptKeyword("...")]`. The parameters are the class's public properties in declaration order. The parent is the first parameter if it references another concept (`[ConceptParent]` can override this). This document is a reference for editor developers (completion, hover, parsing). It lists about 130 keywords in tables and shows the syntax forms that real scripts use.

Legend: **V** = verified in source or wiki (path cited). **I** = inference, not directly verified.

## 1. Facts about the concept classes (V)

| Fact | Value | Source |
|---|---|---|
| Concept source root | `src\Rhetos.CommonConcepts\Dsl.DefaultConcepts\` in the Rhetos repo (github.com/Rhetos/Rhetos) | filesystem |
| Subfolders (files with `ConceptKeyword`) | `Computations` 69, `SimpleBusinessLogic` 56, `DataStructure` 32 (+ `Properties` 12, `Writing` 9), `DatabaseWorkarounds` 32, `RowPermissions` 13, `Advanced` 6 | grep |
| Files with `[ConceptKeyword]` | 229 | grep |
| Distinct keyword spellings | 173 (many keywords are overloaded by several classes) | grep, `sort -u` |
| `*Info.cs` files in that folder tree | 264 (includes non-concept helper classes) | find |
| Overloading | One keyword maps to several classes, distinguished by parameter list: `Take` (3 classes), `Unique` (3), `SqlIndex` (3), `Implements` (3), `Clustered` (3), `Query`/`Load`/`QueryFilter` (prototype and expression variants). | grep |
| Shorthand forms | Classes implementing `IAlternativeInitializationConcept` are shorthand forms (for example `Reference Name;`, `Is Module.Poly;`, `RegExMatch` without message). | source |
| Other code in the package | `Dom.DefaultConcepts`, `DatabaseGenerator.DefaultConcepts`, `Processing.DefaultCommands` implement code generation. | filesystem |
| Built-in scripts | `src\Rhetos.CommonConcepts\DslScripts\` has 6 `.rhe` files (`AutoCode`, `FilterId`, `KeepSynchronizedMetadata`, `Locking`, `Logging`, `Security`). | filesystem |

### XML doc comments on concept classes (V)

Each concept class has a `/// <summary>` describing the keyword. The wiki list reuses the same sentences, so these are the natural hover texts. Members may carry their own `<summary>`.

```csharp
// Dsl.DefaultConcepts\DataStructure\Properties\ReferencePropertyInfo.cs
/// <summary>
///  Lookup field (N:1 relationship). The generated C# property has type of the referenced class.
///  Generates foreign key constraint in database. The database column has "ID" suffix.
/// </summary>
[Export(typeof(IConceptInfo))]
[ConceptKeyword("Reference")]
public class ReferencePropertyInfo : PropertyInfo
{
    public DataStructureInfo Referenced { get; set; }
```

```csharp
// Dsl.DefaultConcepts\Computations\ComposableFilterByInfo.cs  (member-level doc, inline <c> tag)
/// <summary>
/// A lambda expression that returns a subset of a given query with parameter, for example:
/// <c>(query, repository, parameter) => query.Where(...)</c>.
/// </summary>
public string Expression { get; set; }
```

| Doc detail | Observation (V) |
|---|---|
| Tags seen | `<summary>`, `<c>`, XML entities such as `&lt;`. `<remarks>` occurs in 12 files and `<param>` in 2 `*Info.cs` files (grep). |
| Missing docs | 16 of 264 `*Info.cs` files contain no `<summary>`. |
| Base concepts | `DataStructureInfo` is documented as "usually not directly used in DSL scripts". Subclasses such as `EntityInfo` have their own summary. |

## 2. Concept categories

Parent column: the first parameter if it is a concept reference, or "top level" for `Module`. `<X>` means a reference to a concept of type X. In nested form the parent's keys are implicit. Parameter lists come from class definitions (V) unless marked wiki or I.

### 2.1 Modules and data structures

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `Module` | top level | `Name` | C# namespace and DB schema. |
| `DataStructure` | Module | `Name` | Base data structure, rarely used directly. |
| `Entity` | Module | `Name` | Writable data; C# class and DB table. |
| `Browse` | Module | `Name` `<Source>` | Read-only projection of a data structure. |
| `Take` | Browse | `Path`, or `Name` `Path` | Browse property copied via property or reference path. |
| `Hardcoded` | Module | `Name` | Code table with fixed entries. |
| `Entry` | Hardcoded | `Name` [`Identifier`] | One row of a hardcoded entity. |
| `Value` | Entry | `PropertyName` `Value` | Value of an entry column. |
| `LegacyEntity` | Module | `Name` `table` [`view`] | Maps a legacy table or view (wiki). |
| `LegacyProperty`, `LegacyPropertyReadOnly` | LegacyEntity property | `'column'` ... | Column mapping (wiki). |
| `Parameter` | Module | `Name` | Parameter data structure for filters and actions. |

### 2.2 Property types and relationships

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `ShortString` | DataStructure | `Name` | Unicode string up to 256 characters. |
| `LongString` | DataStructure | `Name` | Unlimited multi-line string. |
| `Integer`, `Decimal`, `Money`, `Bool`, `Date`, `DateTime`, `Guid`, `Binary` | DataStructure | `Name` | Simple property types (wiki list; `Money` and `ShortString` classes confirmed). |
| `Reference` | DataStructure | `Name` `<Referenced>`; shorthand `Name` | N:1 lookup; DB column `NameID`. |
| `Detail` | Reference | none | Master-detail (Required, SqlIndex, CascadeDelete). |
| `CascadeDelete` | Reference or UniqueReference | none | Delete related rows with the referenced row. |
| `Extends` | extension DataStructure | `<Base>` | 1:1 extension of a base data structure. |
| `UniqueReference` | DataStructure | `<Base>` | `Reference` with a 1:1 relation. |
| `LinkedItems` | DataStructure | `Name` `<ReferenceProperty>` | Navigation to detail records. |
| `Hierarchy` | Entity | `Name` [`PathName` `GeneratePathFrom` `PathSeparator`] | Self-reference tree, optional cached path. |
| `SingleRoot` | Hierarchy | none | Only one root record (wiki). |
| `PropertyFrom`, `AllPropertiesFrom`, `AllPropertiesWithCascadeDeleteFrom` | DataStructure | `<Source Property or DataStructure>` | Copy properties with Required and index concepts (wiki). |

### 2.3 Simple business rules and validations

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `Required` | Property | none | Value required on save. |
| `Unique` | Property or Entity | none, or property name list (3 variants) | Uniqueness constraint. |
| `MinValue`, `MaxValue` | Property | `Value` | Bounds (`MinValue` V). |
| `MinLength`, `MaxLength` | Property | `Length` | String length limits (`MaxLength` V). |
| `Range` | Entity | `<PropertyFrom>` `<PropertyTo>` | From must not exceed To. |
| `IntegerRange` | Entity | two property names | Integer variant (used in `Book.rhe`). |
| `RegExMatch` | Property | `RegularExpression` [`ErrorMessage`] | Regex validation. |
| `DefaultValue` | Property | `Expression` | Default on insert (lambda). |
| `DenyUserEdit` | Entity or Property | none | Block client writes. |
| `Lock` | Entity | `FilterType` `Title` [`<DependedProperty>`] | Deny update and delete when filter matches. |
| `LockProperty` | Property | `FilterType` `Title` | Deny update of one property. |
| `LockExcept` | Entity | filter, message, excepted properties | Wiki only. |
| `InvalidData` | Entity | `FilterType` `ErrorMessage` [`<DependedProperty>`] | Validation error when filter matches. |
| `MessageParametersConstant` | InvalidData | `MessageParameters` (C# args) | Values for `{0}`, `{1}` in the message. |
| `AutoCode`, `AutoCodeForEach`, `AutoCodeCached`, `AutoCodeForEachCached` | Property | none, or group-by property | Generated codes. |
| `CreationTime`, `ModificationTimeOf`, `CreatedBy` | Property | none, or `<ModifiedProperty>` | Audit columns. |
| `Deactivatable` | Entity | none | Adds `Bool Active`. |
| `PessimisticLocking`, `PessimisticLockingParent` | DataStructure | none, or `<Reference>` | Explicit client locks. |

### 2.4 Filters and read methods

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `FilterBy` | DataStructure | `Parameter` `Expression` | Load method with parameter; parameter type is the filter name. |
| `ComposableFilterBy` | DataStructure | `Parameter` `Expression` | Filter over `IQueryable`. Class comment says to prefer `QueryFilter`. |
| `QueryFilter` | DataStructure | `Parameter` `Expression` (or prototype form) | Newer composable filter. |
| `ItemFilter` | DataStructure | `FilterName` `Expression` | One-line `item => ...` predicate. |
| `Load`, `Query` | DataStructure | type [implementation] | Custom read methods (prototype and expression variants). |
| `ApplyFilterOnClientRead` | DataStructure | `FilterName` [`where`] | Auto-apply filter on web reads. |
| `ApplyOnClientRead` | filter | none | Same, from an item or composable filter (wiki). |
| `FilterByReferenced`, `FilterByLinkedItems`, `FilterByBase` | DataStructure | parameter, reference | Reuse a filter along a relation (wiki). |
| `UseExecutionContext` | Computed, Action, FilterBy | none | Adds `ExecutionContext` parameter (wiki). |

### 2.5 Computations and persisted data

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `Computed` | Module | `Name` `Expression` | Read-only structure from C# code. |
| `SqlQueryable` | Module | `Name` `SqlSource` | Read-only structure from SQL (DB view). |
| `QueryableExtension` | Module | `Name` `<Source>` `Expression` | LINQ-based extension (wiki). |
| `Persisted` | Module | `Name` `<Source>` | Cached entity filled from a source. |
| `ComputedFrom` | Entity or Property | `<Source DataStructure>`, or `<Source Property>` | Flexible cache with property-level recompute. |
| `AllProperties` | Persisted, ComputedFrom, History, Logging | none | Include all properties. Overloaded on four parents. |
| `KeyProperties` | Persisted, ComputedFrom | `KeyProperties` | Matching key (default ID). |
| `ChangesOnBaseItem` | computation | none | Recompute when the base record changes. |
| `ChangesOnLinkedItems` | computation | `<LinkedItemsReference>` | Recompute on detail change. |
| `ChangesOnReferenced` | computation | `ReferencePath` | Recompute on referenced change. |
| `ChangesOnChangedItems` | computation | entity, type, snippet | Custom dependency (wiki; used in `Action.rhe`). |
| `ExternalReference` | Module | type or assembly | Adds a DLL reference (wiki). |
| `ReportData`, `ReportFile`, `DataSources` | Module | see wiki | Reports (`ReportData` extends `ParameterInfo`). |

### 2.6 Actions and low-level object model

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `Action` | Module | `Name` `Script` | Server action running C# code; body holds parameter properties. |
| `BeforeAction` | Action | `RuleName` `CodeSnippet` | Code before the action. |
| `SaveMethod` | Entity | none (container for snippets) | Hook into `Save`. |
| `RepositoryUses` | DataStructure | `PropertyName` `PropertyType` | Inject a dependency into the repository. |
| `RepositoryMember` | DataStructure | `Name` `Implementation` | Add arbitrary repository code. |
| `Implements` (interface) | DataStructure | `InterfaceType` | POCO implements a C# interface. |
| `RegisteredImplementation` | Implements | none | Default implementation of an interface. |
| `Write` | DataStructure | `'saveImplementation'` | Custom save for non-writable structure (wiki). |

### 2.7 Polymorphic

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `Polymorphic` | Module | `Name` | Common interface for several entities. |
| `Is` | subtype DataStructure | `<Supertype>` [`ImplementationName`] | Marks a subtype. |
| `Implements` (property) | Is | `<Property>` `Expression`, or `<Property>` `<Entry>` | Property implemented by SQL or hardcoded entry. |
| `Where`, `Materialized`, `SqlImplementation` | Is or Polymorphic | see wiki | Subtype filter, table, custom SQL. |

### 2.8 Permissions and row permissions

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `CustomClaim` | top level | `ClaimResource` `ClaimRight` | Extra security claim. |
| `RowPermissions` | DataStructure | `SimplifiedExpression`, or none | Row-level access rules (2 variants). |
| `RowPermissionsRead`, `RowPermissionsWrite` | DataStructure | `SimplifiedExpression` | Read or write row filter. |
| `AllowRead`, `AllowWrite`, `Allow` | RowPermissions (block form, without parameters) | `Name` `FilterExpressionFunction` (C# `context => { ...; return item => ...; }`; V: Bookstore `DslSyntax.json`, `DemoRowPermissions2.rhe`) | Named permission rule. |

### 2.9 History and logging

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `Logging` | Entity | none | DB trigger logging to `Common.Log`. |
| `Log` | Logging | `<Property>` | Property to track (wiki). |
| `RelatedItem` | Logging | parent table, column, relation | Link log records (wiki). |
| `History` | Entity or Property | none | Temporal data. |

### 2.10 SQL objects

| Keyword | Parent | Parameters | Purpose |
|---|---|---|---|
| `SqlObject` | Module | `Name` `CreateSql` `RemoveSql` | Generic DB object with create and drop scripts; `{SPLIT SCRIPT}` separator in `SqlWorkarounds.rhe`. |
| `SqlView` | Module | `Name` `ViewSource` | DB view. |
| `SqlFunction` | Module | `Name` `Arguments` `Source` (or single full script) | DB function. |
| `SqlProcedure` | Module | `Name` `ProcedureArguments` `ProcedureSource` (or single) | Stored procedure. |
| `SqlTrigger` | DataStructure | `Name` `Events` `TriggerSource` | DB trigger. |
| `SqlIndex` | Property or Entity | none, or property name lists (3 variants) | Index. |
| `SqlIndexMultiple` | DataStructure | `PropertyNames` | Multi-column index. |
| `Clustered` | SqlIndexMultiple, Unique | none (3 variants) | Clustered index. |
| `SqlDefault` | Property | `Definition` | Default constraint. |
| `SqlNotNull` | Property | `InitialValueSqlExpression` | Internal NOT NULL. |
| `SqlDependsOn` | any concept | `<Property>`, `<DataStructure>` or `<Module>` | Creation order. |
| `SqlDependsOnID`, `SqlDependsOnIndex`, `SqlDependsOnView`, `SqlDependsOnFunction`, `SqlDependsOnSqlObject` | any | target | Same for other targets (wiki). |
| `AutodetectSqlDependencies` | Module, SqlQueryable, SqlFunction, SqlProcedure, LegacyEntity | none | Parses SQL to infer dependencies. |

## 3. Syntax patterns in practice

Rules from `Wiki\Rhetos-DSL-syntax.md` (V): statement = keyword, parameters, then `;` or `{ ... }`. Keywords are case-insensitive. Strings with spaces or special characters need `'` or `"` quotes, and a quote inside a string is escaped by doubling it. Any string parameter can be an external file in angle brackets, relative path (`<RegexMessage.txt>`). `//` line comments and `/* */` block comments (since Rhetos 5.4). Statement order and duplicates are irrelevant.

**Nested versus flat form** (V, wiki). Nesting only fills in the first argument from the parent.
```
Module Demo { Entity Person { ShortString Name; } }      // nested
Module Demo; Entity Demo Person; ShortString Demo.Person Name;   // flat (the wiki still shows the obsolete form Entity Demo.Person; ShortString Demo.Person.Name;)
```
Compact one-line form: `Module TestDataStructure { DataStructure SimpleDataStructure2 { ShortString SimpleShortString; } }` (`Rhetos6\test\CommonConcepts.TestApp.Shared\DslScripts\DataStructure.rhe`).

**Typical entity script** (`Rhetos6\test\TestAppDirectMsSqlEf6\DslScriptsTest\Book.rhe`, shortened):
```
Module Bookstore
{
    AutodetectSqlDependencies;
    Entity Book
    {
        ShortString Code { AutoCode; }
        ShortString Title { Required; }
        Reference Author Bookstore.Person;
    }
    Entity Comment { Reference Book { Detail; } LongString Text; }
    Browse BookGrid Bookstore.Book { Take Code; Take 'Author.Name'; Take TranslatorName 'Extension_ForeignBook.Translator.Name'; }
}
```

**C# in string parameters** (`...\CommonConcepts.TestApp.Shared\DslScripts\Action.rhe`). Single quotes around C# avoid escaping its double quotes. Strings span lines.
```
Action InsertAndThrowException
    '(parameters, repository, userInfo) =>
        {
            repository.TestAction.ToInsert.Insert(new TestAction.ToInsert { ID = parameters.ItmemID.Value, Name = "Test" });
            throw new Rhetos.UserException("TestAction exception " + parameters.Message);
        }'
{
    ShortString Message;
    Guid ItmemID;
}
```
SQL snippets use double quotes: `SqlObject DependencyA "PRINT 'Create'" "PRINT 'Remove'" { SqlDependsOn TestSqlWorkarounds.DependencyBase.A; }` (`SqlWorkarounds.rhe`).

**`ItemFilter` with `InvalidData`, `Lock`** (`Validations.rhe`):
```
ItemFilter TooMuch 'item => item.Count > 100';
InvalidData TooMuch '[Test] Quantity may not be larger than 100.';
ItemFilter ContainsLockMark 'item => item.Name.Contains("lock")';
Lock ContainsLockMark '[Test] Name contains lock mark.';
LockProperty Name.CountNegative 'Name is locked if count negative.';
```

**`FilterBy`, `ComposableFilterBy`, `Parameter`** (`Reading.rhe`):
```
Parameter FilterA;
Entity Simple {
    ShortString Name;
    FilterBy FilterA '(repository, parameter) => this.CustomFilterA()';
    ComposableFilterBy FilterB '(query, repository, parameter) => this.CustomFilterB(query)';
}
```

**`SqlQueryable` with multiline SQL and body** (`Action.rhe`; `ChangesOnChangedItems` is in the same file):
```
SqlQueryable ComputeNameContainsA
"
    SELECT ID, ContainsA = CASE WHEN Name LIKE '%A%' THEN CONVERT(BIT, 1) ELSE CONVERT(BIT, 0) END
    FROM TestAction.Simple
"
{
    AutodetectSqlDependencies;
    Extends TestAction.Simple;
    ChangesOnChangedItems TestAction.Simple 'Guid[]' 'changedItems => changedItems.Select(item => item.ID).ToArray()';
    Bool ContainsA;
}
Persisted NameContainsA TestAction.ComputeNameContainsA ...   // continues in the file
```

**SQL in a file reference.** `SqlQueryable BookDescription <BookDescription.sql> { Extends Bookstore.Book; LongString Description; }` (`Book.rhe`). Built-in scripts use spaces in file names: `<SQL\AutoCodeCacheGetNext create.sql>` (`Rhetos6\src\Rhetos.CommonConcepts\DslScripts\AutoCode.rhe`).

**`Computed` with C# body and nested `FilterBy`** (`Book.rhe`): `Computed ComputeBookRating 'repository => { ... }' { Extends Bookstore.Book; Decimal Rating; FilterBy 'IEnumerable<Guid>' '(repository, bookIds) => ...'; }`.

**Polymorphic** (`Polymorphic.rhe`):
```
Polymorphic SimpleBase { ShortString Name; Integer Days; }
Entity Simple2 { ShortString Name1; Integer Name2; DateTime Finish;
    Is TestPolymorphic.SimpleBase {
        Implements TestPolymorphic.SimpleBase.Name "Name1 + '-' + CONVERT(NVARCHAR(256), Name2)"; } }
```

**Row permissions** (`RowPermissions.rhe`): `RowPermissionsRead '(source, repo, context) => item => (item.value >= 1500) && (item.value <= 2500)';`

**Patterns from a real application** (Bookstore, github.com/Rhetos/Bookstore `master`, Rhetos 6.0.0; files in `Bookstore\src\Bookstore.Service\DslScripts\`, shortened, enclosing `Module`/`Entity` omitted; V):
```
CustomClaim "DemoRowPermissions2.Document" "DemoCustomRead";   // top level, outside any Module (AdditionalExamples\DemoRowPermissions2.rhe)
Module Common { Entity Principal { ShortString NewBookNotificationEmailAddress; } }   // extends a Rhetos.CommonConcepts entity (MailNotifications.rhe)
Hardcoded ShipmentStatus { Entry Preparing; Entry Approved; Entry DeliveryInProgress; Entry Delivered; }   // ShipmentState.rhe
Entity Shipment {
    Is Bookstore.ShipmentEvent 'Creating' {                                               // Is with an ImplementationName (Shipment.rhe)
        Implements Bookstore.ShipmentEvent.EffectiveSince "CreatedAt";                    // SQL expression
        Implements Bookstore.ShipmentEvent.NewStatus Bookstore.ShipmentStatus.Preparing;  // reference to a hardcoded Entry
    } }
RowPermissions {                                                         // block form, inside Entity Document (DemoRowPermissions2.rhe)
    Allow WithinDivision 'context => { ...; return item => item.Division.ID == myDivisionId; }';
    AllowRead SupervisedRegions 'context => { ...; return item => myRegionIds.Contains(item.Division.Region.ID); }'; }
InvalidData ImportantBookExplanation '...' { MarkProperty Bookstore.Disposal.Explanation; ErrorMetadata 'Severity' 'Low'; }   // Disposal.rhe
Browse BookGrid Bookstore.Book { Take Code; Query 'Bookstore.WantedBooks' 'parameter => { ... }'; }   // AdditionalExamples\ExampleFilters.rhe
ComposableFilterBy 'System.String[]' '(query, repository, titles) => ...';   // parameter named by a C# type (same file)
SaveMethod { LoadOldItems { Take Score; Take 'Book.Title'; } OnSaveUpdate AppendTextIfScoreChanged '...'; }   // Review.rhe
```
- The two `Implements` statements are different concepts with the same parameter count: `SubtypeImplementsPropertyInfo` has a string `Expression`, and
  `SubtypeImplementsReferenceToHardcodedEntityInfo` has an `Entry` reference. The token shape after the property reference decides which one parses (I).
- `Entity Book` occurs 7 times in 4 files (`Book.rhe`, `MailNotifications.rhe`, `SalesItem.rhe`, and 4 times in `AdditionalExamples\ExampleFilters.rhe`).
  Each occurrence adds members; identical repeated statements such as `ShortString Code { AutoCode; }` are accepted.
- App-specific keywords come from the app's concept project `Bookstore.RhetosExtensions` (a `ProjectReference`): `Deactivatable { DeactivateOnDelete; }`,
  `PhoneNumber MobilePhone;`, `CodeTable;`, `ComplexProperty Book Bookstore.Book;`. They exist only in this app's `DslSyntax.json`.
- A doubled quote inside a quoted C# snippet: `"I don''t like it"` (`Review.rhe`). 7 of the 17 files start with a UTF-8 BOM, `Review.rhe` and
  `ExampleComputed.rhe` contain tab characters, and no file uses `/* */` comments.

**Test fixtures of this repo.** `test\RhetosMockAppContents\RhetosAppFolder\...\doc.rhe` files are mock files (the `doc.rhe` files are empty except one with `Entity e;`; `directive.rhe` contains only a `// <rhetosProjectRootPath="..." />` comment), so they are not realistic DSL samples.

## 4. Implications for the editor (I unless marked V)

| Topic | Note |
|---|---|
| Keyword overloading | A keyword alone does not identify the concept class. Match by parameter count and parent context (V for `Take`, `Unique`, `SqlIndex`). |
| Case | Keywords are case-insensitive (V, wiki). |
| Concept discovery | Read concepts from `obj/Rhetos/DslSyntax.json` and `<summary>` text from `obj/Rhetos/DslDocumentation.json` (both generated by the build from the plugin assemblies and XML docs). Use this list only as test expectations. |
| Strings | Multi-line `'...'` or `"..."` hold C# or SQL (embedded-language candidates). `<file>` parameters are file references. |
| Modules | Several `Module X { }` blocks with the same name occur in one file (V: `DataStructure.rhe`). |
| Not covered | Concepts from other packages (`Rhetos.MsSql*`, `PostgreSql`, `EfCore`), the long tail of the 173 keywords, `Reports` and `Legacy*` details. |

## 5. Sources read

| Source | Used for |
|---|---|
| `Wiki\List-of-DSL-concepts-in-CommonConcepts.md` | keyword list and purposes (primary) |
| `Wiki\Rhetos-DSL-syntax.md` | syntax rules |
| `Rhetos6\src\Rhetos.CommonConcepts\Dsl.DefaultConcepts\**\*Info.cs` | keywords, `[ConceptKey]` members, counts |
| `Rhetos6\test\CommonConcepts.TestApp.Shared\DslScripts\*.rhe`, `Rhetos6\test\TestAppDirectMsSqlEf6\DslScriptsTest\Book.rhe` | examples |
| `Rhetos6\src\Rhetos.CommonConcepts\DslScripts\*.rhe` | built-in scripts |
| `LanguageServices\test\RhetosMockAppContents\...\*.rhe` | mock test files |
| Bookstore (github.com/Rhetos/Bookstore `master`, Rhetos 6.0.0): `Bookstore\src\Bookstore.Service\DslScripts\**\*.rhe`, `Bookstore\src\Bookstore.Service\obj\Rhetos\DslSyntax.json` | real-application patterns (§3), members of `Allow`/`AllowRead`/`AllowWrite` (§2.8) |
