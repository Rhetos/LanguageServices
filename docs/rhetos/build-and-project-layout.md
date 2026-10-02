# Rhetos application: on-disk layout and build pipeline (Rhetos 4, 5, 6)

A Rhetos application is an ordinary `.csproj` (SDK-style in v5+) with NuGet `PackageReference`s to Rhetos packages. When the project builds, the targets from the
`Rhetos.MSBuild` package do three things. First, an in-process MSBuild task writes `obj/Rhetos/rhetos-project.assets.json`, which lists every
NuGet package and project reference, its files, and the plugin DLLs. Second, the targets run `rhetos build` (Rhetos CLI). This command scans the
plugin DLLs, generates `DslSyntax.json` and `DslDocumentation.json`, parses all `.rhe` scripts, and writes C# sources to `obj/Rhetos/Source` and JSON
assets to `obj/Rhetos/Assets`. Third, MSBuild compiles those sources together with the hand-written C# code. Optionally, a target from the
`Rhetos.Host` package then runs `rhetos dbupdate` against the database. Only `dbupdate` needs a database. The CLI and MSBuild integration have
existed since Rhetos 4. `DslSyntax.json` exists since Rhetos 5 (on Rhetos 4 only through the `Rhetos.LanguageServicesCompatibility` plugin). The
Language Services server needs only `obj/Rhetos/DslSyntax.json` (and optionally `DslDocumentation.json`).

**Sources and conventions.** Plain paths are relative to the Rhetos repo root (github.com/Rhetos/Rhetos, commit in README Sources; working tree is `6.1.1-dev`, which targets
`net10.0`). `git:<tag>:<path>` means the file as read with `git show` at that release tag. `Wiki/`, `LS/`, `LSC/`, `Bookstore/` and `Bookstore v4/` are
relative to the root of the corresponding repository (`LS` = LanguageServices, `LSC` = LanguageServicesCompatibility, `Bookstore` = github.com/Rhetos/Bookstore
at branch `master` on Rhetos 6.0.0, `Bookstore v4` = the same repository at its Rhetos 4.3 revision `afa98a5`; see README Sources). Each statement is **verified** in
code or in a built sample app unless it is marked *(inferred)* or *(unverified)*. Built samples examined: `test/CommonConcepts.TestApp.MsSql`
(v6 EF Core), `test/CommonConcepts.TestApp.MsSqlEf6`, `test/TestAppDirectMsSqlEfCore`, `Bookstore/src/Bookstore.Service` (released 6.0.0, EF6, net8.0),
`Bookstore v4/src/Bookstore.Service` (v4.3).

## 1. On-disk layout

### 1.1 Project folder (source side)

| Item | Purpose | Evidence |
|---|---|---|
| `<App>.csproj` (`PackageReference`; SDK-style in v5+) | Build requires `obj/project.assets.json` (no `packages.config`) and exactly one target framework (multi-targeting throws). The v6 Bookstore uses `Microsoft.NET.Sdk.Web`; the v4 Bookstore uses a legacy non-SDK web-application `.csproj` (net472) with `PackageReference` | `src/RhetosVSIntegration/NuGetUtilities.cs`, `Bookstore/src/Bookstore.Service/Bookstore.Service.csproj`, `Bookstore v4/src/Bookstore.Service/Bookstore.Service.csproj` |
| `Rhetos.MSBuild` package ref | Adds the build targets and `tools/rhetos.dll` | `src/Rhetos.MSBuild.nuspec` |
| `Rhetos.Host` package ref | Adds the dbupdate target and copies `rhetos.exe`/`rhetos.dll` to `bin` | `src/Rhetos.Host.nuspec` |
| `Rhetos`, `Rhetos.CommonConcepts` + DB provider (`Rhetos.MsSql`, `Rhetos.MsSqlEf6` or `Rhetos.PostgreSql`) | Framework, standard DSL, SQL dialect. v6 build fails without a provider | `src/Rhetos.Core/Configuration.Autofac/ApplicationBuild.cs` (`ISqlUtility` check) |
| `**/*.rhe` | DSL scripts; anywhere under the project folder (v5.3+) | `src/RhetosVSIntegration/MsBuildIntegrationBuild/Rhetos.MSBuild.targets`, `ChangeLog.md` 5.3.0 |
| `**/DataMigration/**` | Data-migration SQL scripts | same targets file |
| `Resources/Rhetos/**` | Legacy assets; copied only if `Rhetos:Build:BuildResourcesFolder=true` | `src/Rhetos.Core/Deployment/ResourcesGenerator.cs` |
| `rhetos-build.settings.json` | Build options (`Rhetos:Build:*`, `Rhetos:Subpackages`, `CommonConcepts:*`) | `src/RhetosCli/Program.cs` (`Build`) |
| `rhetos-dbupdate.settings.json` | dbupdate options | `src/Rhetos.Core/Utilities/DbUpdateOptions.cs` |
| Runtime settings | v4: `rhetos-app.settings.json` + `rhetos-app.local.settings.json`, auto-loaded. v5+: not auto-loaded; the app configures its own host (`appsettings.json`, `local.settings.json`, `ConnectionStrings:RhetosConnectionString`). Example: the v6 Bookstore adds its gitignored `rhetos-app.local.settings.json` explicitly (`.AddJsonFile("rhetos-app.local.settings.json")`); the template of that file holds `ConnectionStrings:RhetosConnectionString` | `ChangeLog.md` 5.0.0 "Changes in behavior" 3–5; `Bookstore/src/Bookstore.Service/Startup.cs`, `Bookstore/tools/Configs/Templates/Template.rhetos-app.local.settings.json` |

Concrete v6 example, `test/CommonConcepts.TestApp.MsSql/CommonConcepts.TestApp.MsSql.csproj` (abridged):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Rhetos.Host" Version="6.1.1-dev*" />
    <PackageReference Include="Rhetos.MSBuild" Version="6.1.1-dev*" />
    <PackageReference Include="Rhetos" Version="6.1.1-dev*" />
    <PackageReference Include="Rhetos.CommonConcepts" Version="6.1.1-dev*" />
    <PackageReference Include="Rhetos.MsSql" Version="6.1.1-dev*" />
  </ItemGroup>
  <Import Project="..\CommonConcepts.TestApp.Shared\CommonConcepts.TestApp.Shared.projitems" Label="Shared" />
</Project>
```

This sample keeps its `.rhe` files in a shared project (`../CommonConcepts.TestApp.Shared/DslScripts`), outside the project folder.
`rhetos-build.settings.json` declares that folder as a `Subpackage` because "DiskDslScriptLoader will not load its own project's DSL scripts
that are not within the project's folder" (comment in `test/CommonConcepts.TestApp.MsSql/rhetos-build.settings.json`).

Released 6.0.0 example: `Bookstore/src/Bookstore.Service/Bookstore.Service.csproj` (`Microsoft.NET.Sdk.Web`, `net8.0`) references `Rhetos`, `Rhetos.CommonConcepts`,
`Rhetos.MSBuild`, `Rhetos.Host`, `Rhetos.Host.AspNet`, `Rhetos.MsSqlEf6`, `Rhetos.RestGenerator`, `Rhetos.Impersonation` and `Rhetos.Host.AspNet.Impersonation`
(all 6.0.0), plus a `ProjectReference` to the app's concept library `Bookstore.RhetosExtensions`. All 17 `.rhe` files are under the project's `DslScripts/`
folder (6 of them in `DslScripts/AdditionalExamples/`), and `rhetos-build.settings.json` is `{}`, so the app needs no `Subpackage`.

### 1.2 `obj/Rhetos/` inventory (v6, from a built `CommonConcepts.TestApp.MsSql`)

| File | Producer | Content / notes |
|---|---|---|
| `rhetos-project.assets.json` | MSBuild task `ResolveRhetosProjectAssets` (`src/RhetosVSIntegration/ResolveRhetosProjectAssets.cs`) | Packages, content files, plugin DLLs (see 1.3). Rewritten only if the content changed, but its timestamp is **always** updated ("to notify the language server", same file) |
| `Rhetos.BuildItems` | target `ResolveRhetosProjectAssets` (`WriteLinesToFile`) | List of `RhetosBuild` item paths + settings file; incremental-build input |
| `Rhetos.BuildComplete` | target `BuildRhetosApp` | Empty marker, deleted before and written after `rhetos build`; the only `RhetosOutput` |
| `Rhetos.ExternalSourceItems` | `rhetos build` (`src/Rhetos.Core/Configuration.Autofac/AdditionalSourceFilesListing.cs`) | External files (e.g. `.sql`) referenced from DSL; read back as build inputs. v6+ only |
| `RhetosGeneratedSourceFiles.txt` | target `AddRhetosSourceFiles` | Full paths of generated `.cs` files. The LS VS extension reads its timestamp (`LS/src/VisualStudioExtensionShared/SolutionRefreshService.cs`) |
| `Rhetos.DatabaseUpdated` | target `DeployRhetosApp` (`src/RhetosVSIntegration/MsBuildIntegrationDbUpdate/Rhetos.Host.targets`) | Empty marker after a successful dbupdate |
| `DslSyntax.json` | `DslSyntaxFileGenerator` (`src/Rhetos.Core/Dsl/DslSyntaxFileGenerator.cs`, `src/Rhetos.Core.DslParser/DslSyntaxFile.cs`) | Concept types and members from **plugins only** (`DslSyntaxFromPlugins.cs`), not from scripts. Written only if the content changed |
| `DslDocumentation.json` | `DslDocumentationFileGenerator` | Concept summaries from the plugin DLLs' XML doc files. Written only if the content changed |
| `PluginScanner.json` | `src/Rhetos.Core/Extensibility/PluginScannerCache.cs` | Cache of plugin types per assembly |
| `MacroOrder.json` | `src/Rhetos.Core/Dsl/MacroOrderRepository.cs` | Cache of macro evaluation order |
| `Source/**/*.cs` | `DomGenerator` + other generators via `ISourceWriter` (`src/Rhetos.Core/Compiler/SourceWriter.cs`) | Generated object model (`Model/*Model.cs`, `Repositories/*Repositories.cs`, `EntityFrameworkContext.cs`, …; 166 files in the sample). Obsolete files are deleted; files are read-only since 5.4 |
| `Assets/DslModel.json` | `DslModel` → `DslModelFile.SaveConcepts` | Parsed and macro-expanded concept model |
| `Assets/DatabaseModel.json` | `DatabaseModelGenerator` | SQL create/remove scripts per concept |
| `Assets/DataMigrationScripts.json` | `DataMigrationScriptsGenerator` | Collected `DataMigration` scripts |
| `Assets/ConceptDataMigrationScripts.json` | `ConceptDataMigrationGenerator` | Data migration generated from concepts |
| `Assets/InstalledPackages.json` | `InstalledPackagesGenerator` | Runtime copy of the package list |
| `Assets/ServerDomEdm.csdl/.msl/.ssdl` | `EntityFrameworkMappingGenerator` (`src/Rhetos.MsSqlEf6/Core/`) | EF6 apps only |
| `Assets/<Package>/…` | `ResourcesGenerator` | Only with `BuildResourcesFolder=true` |
| `LinqPad/Rhetos DOM.linq` | target `RhetosGenerateLinqPadScripts` (Rhetos.Host.targets) | LINQPad script; v5+ |

`rhetos build` empties `Assets/` at the start of every run. It does not delete the cache files (`DslSyntax.json`, `PluginScanner.json`, …) (`src/Rhetos.Core/Deployment/ApplicationGenerator.cs`).

The released 6.0.0 Bookstore (`Bookstore/src/Bookstore.Service/obj/Rhetos`) has every file of this table except `Assets/<Package>/…`, including the EF6 files
`Assets/ServerDomEdm.csdl/.msl/.ssdl`. Its `Rhetos.ExternalSourceItems` lists the app's three `DslScripts/SQL/*.sql` files and 11 SQL files from the
`Rhetos.CommonConcepts` package folder in the NuGet cache. Only the app project has `obj/Rhetos`; the concept library, test and playground projects of the
solution have none (see section 6).

### 1.3 `rhetos-project.assets.json` schema

The model classes are in `src/Rhetos.Core.Integration/Deployment/` (`RhetosProjectContent`, `RhetosProjectAssets`, `InstalledPackage`,
`ContentFile`, `PackageRequest`) and `src/Rhetos.Core.Integration/Utilities/RhetosBuildEnvironment.cs`. The path is hard-coded as
`<project>/obj/Rhetos/rhetos-project.assets.json` (`RhetosProjectContentProvider.cs`). Serialization uses Newtonsoft with indentation; the file
was 550 KB in the sample (868 KB, with 78 packages and 69 assemblies, in the v6 Bookstore). It is written with `File.WriteAllText(…, Encoding.UTF8)`, so,
unlike `DslSyntax.json`, it starts with a UTF-8 BOM (`EF BB BF`; `RhetosProjectContentProvider.cs`; observed in the v4 and v6 Bookstore and in the Rhetos test apps).

| JSON path | Meaning |
|---|---|
| `RhetosBuildEnvironment.ProjectFolder` | Absolute project folder |
| `.OutputAssemblyName` | `$(AssemblyName)` |
| `.CacheFolder` | `<project>\obj\Rhetos` (contains `DslSyntax.json`) |
| `.GeneratedAssetsFolder` / `.GeneratedSourceFolder` | `obj\Rhetos\Assets` / `obj\Rhetos\Source` |
| `RhetosTargetEnvironment.{TargetPath,TargetAssetsFolder}` | **v4 only** (`git:v4.3.0:Source/Rhetos.Utilities/RhetosTargetEnvironment.cs`). The v4 Bookstore has `…\bin\Bookstore.Service.dll` and `…\bin\RhetosAssets`; the v6 Bookstore has no `RhetosTargetEnvironment` |
| `RhetosProjectAssets.InstalledPackages.Packages[]` | Topologically sorted by dependencies; the **current project is last**, with `Version: ""` |
| `…Packages[].{Id,Version,Dependencies[{Id,VersionsRange}],Folder}` | NuGet id, version, dependency ranges, package-cache folder |
| `…Packages[].ContentFiles[{PhysicalPath,InPackagePath}]` | NuGet packages: every file of the package. Project: the `RhetosBuild` items (InPackagePath may start with `..\`) |
| `RhetosProjectAssets.Assemblies[]` | Absolute paths of the packages' runtime DLLs + output DLLs of referenced projects; these are scanned for plugins |

```json
{ "RhetosBuildEnvironment": { "ProjectFolder": "C:\\…\\CommonConcepts.TestApp.MsSql", "OutputAssemblyName": "CommonConcepts.TestApp.MsSql",
    "CacheFolder": "…\\obj\\Rhetos", "GeneratedAssetsFolder": "…\\obj\\Rhetos\\Assets", "GeneratedSourceFolder": "…\\obj\\Rhetos\\Source" },
  "RhetosProjectAssets": { "InstalledPackages": { "Packages": [
      { "Id": "Rhetos.CommonConcepts", "Version": "6.1.1.0", "Dependencies": [ { "Id": "Rhetos", "VersionsRange": "[6.1.1-dev…, 6.2.0)" } ],
        "Folder": "C:\\Users\\…\\.nuget\\packages\\rhetos.commonconcepts\\6.1.1-dev…",
        "ContentFiles": [ { "PhysicalPath": "…\\DslScripts\\AutoCode.rhe", "InPackagePath": "DslScripts\\AutoCode.rhe" } ] },
      { "Id": "CommonConcepts.TestApp.MsSql", "Version": "", "Folder": "C:\\…\\CommonConcepts.TestApp.MsSql\\.",
        "ContentFiles": [ { "PhysicalPath": "C:\\…\\CommonConcepts.TestApp.Shared\\DslScripts\\Action.rhe",
                            "InPackagePath": "..\\CommonConcepts.TestApp.Shared\\DslScripts\\Action.rhe" } ] } ] },
    "Assemblies": [ "C:\\Users\\…\\rhetos.mssql\\6.1.1-dev…\\lib\\net10.0\\Rhetos.MsSql.dll", "…" ] } }
```

Observed in `test/TestAppDirectMsSqlEfCore`: a `ProjectReference` appears as a package with `Version: "1.0.0.0"` and **0 ContentFiles**. Its DLL
is listed in `Assemblies`, but its `.rhe` files are not picked up. That app therefore adds them with explicit `RhetosBuild` items and a
`Subpackage` (its `.csproj` and `rhetos-build.settings.json`).

The v6 Bookstore shows the same for its concept library. `Bookstore.RhetosExtensions` (a `ProjectReference` without `.rhe` files) is listed with
`Version: "1.0.0.0"` (the NuGet project version; its `AssemblyVersion` is 5.0.0), `Folder` = its project folder, and 0 `ContentFiles`. `Assemblies` contains its
build output `src\Bookstore.RhetosExtensions\bin\Debug\net8.0\Bookstore.RhetosExtensions.dll`, and its 4 concepts (`CodeTable`, `ComplexProperty`,
`DeactivateOnDelete`, `PhoneNumber`) are in `DslSyntax.json`. SQL files referenced from the app's own scripts (`<SQL\BookDescription.sql>`) are not
`ContentFiles`, because only `.rhe`, `DataMigration` and `Resources` files are `RhetosBuild` items; they appear only in `Rhetos.ExternalSourceItems`.

### 1.4 `bin/<Configuration>/<tfm>/` (v6)

| Item | Origin |
|---|---|
| `<App>.dll` (contains the generated code), plugin DLLs | normal compile and copy |
| `rhetos.exe`, `rhetos.dll`, `rhetos.deps.json`, `rhetos.runtimeconfig.json`, `rhetos.exe.nlog` | `Rhetos.Host` contentFiles `RhetosTools/*`, flattened (`src/Rhetos.Host.nuspec`) |
| `RhetosAssets/` | copy of `obj/Rhetos/Assets` (target `MarkRhetosGeneratedFilesToCopyToOutput`) |
| `RhetosDebugSource/` | copy of `obj/Rhetos/Source` (same target) |
| `LinqPad/Rhetos DOM.linq` | `RhetosGenerateLinqPadScripts` |
| `Logs/RhetosCli.log`, `Logs/RhetosCliPerformance.log` | dbupdate logs (`src/RhetosCli/rhetos.exe.nlog`). Build logs go to the NuGet cache `tools/Logs/RhetosCli-<ProjectFolderName>.log` *(inferred from `${basedir}` + `rhetos-log-suffix` in `Program.cs`)* |
| `EfMappingGeneratedViews.json` | EF6 only; written by dbupdate (comment in Rhetos.Host.targets `AfterClean`) |

## 2. Build pipeline

### 2.1 MSBuild targets (v5/v6; `Rhetos.MSBuild.targets` is identical in `git:v6.0.0` and the working tree)

| # | Target (file) | Hook | Condition / incremental | Action |
|---|---|---|---|---|
| 1 | `ResolveRhetosBuildItems` (MSBuild.targets) | `BeforeTargets="ResolveRhetosProjectAssets;CollectUpToDateCheckInputDesignTime"` | always | `RhetosBuild` items = `**\*.rhe`, `**\DataMigration\**\*`, `Resources\Rhetos\**\*`, `*\**\Resources\**\*`; reads `Rhetos.ExternalSourceItems`; sets VS `UpToDateCheckInput` |
| 2 | `ResolveRhetosProjectAssets` | `DependsOnTargets="ResolveLockFileReferences;ResolveAssemblyReferences;ResolveReferences"`, `BeforeTargets="BeforeCompile"` | **no condition: also runs in design-time builds** | Writes `rhetos-project.assets.json` and `Rhetos.BuildItems` |
| 3 | `ResolveBuildRhetosAppTargetInput` | dependency of 4 | – | `RhetosInput` = `Rhetos.BuildItems`, `obj/project.assets.json`, `rhetos-build.settings.json`, `@(RhetosBuild)`, `@(ReferencePath)`, external items |
| 4 | `BuildRhetosApp` | `BeforeTargets="BeforeCompile"` | `RhetosBuild==True`, not `DesignTimeBuild`, `BuildingProject`; `Inputs=@(RhetosInput)` `Outputs=Rhetos.BuildComplete` | `dotnet "<pkg>\tools\rhetos.dll" --msbuild-format build "<ProjectDir>."` (optional `--fx-version`/`--roll-forward` from `RhetosBuildDotNetFxVersion`/`RhetosBuildDotNetRollForward`) |
| 5 | `AddRhetosSourceFiles` | `BeforeTargets="BeforeCompile"` | – | Adds `obj\Rhetos\Source\**\*.cs` to `Compile`; writes `RhetosGeneratedSourceFiles.txt` |
| 6 | `MarkRhetosGeneratedFilesToCopyToOutput` | `BeforeTargets="AssignTargetPaths"` | – | Copies Assets → `bin\RhetosAssets`, Source → `bin\RhetosDebugSource` |
| – | `CoreCompile` (SDK) | runs after `BeforeCompile` *(standard MSBuild target order, not in the Rhetos repo)* | – | Compiles hand-written and generated C# |
| 7 | `RhetosGenerateLinqPadScripts` (Host.targets) | `BeforeTargets="AssignTargetPaths"` | – | `obj\Rhetos\LinqPad\Rhetos DOM.linq` |
| 8 | `DeployRhetosApp` (Host.targets) | `AfterTargets="Build"` | `RhetosDeploy==True`; `Inputs=$(TargetDir)**\*` `Outputs=Rhetos.DatabaseUpdated` | `dotnet "$(TargetDir)rhetos.dll" --msbuild-format dbupdate "$(TargetDir)\<AssemblyName>.dll"` |

The properties `RhetosBuild` and `RhetosDeploy` default to `True` (`Rhetos.MSBuild.props`, `Rhetos.Host.props`). Output messages that contain
`[Error]` or `[Warn]`/`[Warning]` become MSBuild errors or warnings. With `--msbuild-format`, a `DslSyntaxException` is printed in canonical form
`<file>(<line>,<col>): error <code>: <message>` (`src/RhetosCli/Program.cs` `PrintCanonicalError`).

### 2.2 What `rhetos build` does (`src/RhetosCli/Program.cs` `Build`, `src/Rhetos.Core/Configuration.Autofac/ApplicationBuild.cs`)

1. Loads `obj/Rhetos/rhetos-project.assets.json`; fails with "MSBuild should pass first to create this file" if it is missing.
2. Refuses to run from inside the project's `bin` folder (the build must use the copy in the NuGet `tools` folder).
3. Builds the configuration: `RhetosBuildEnvironment` from the assets file, a few defaults, and `rhetos-build.settings.json` (optional).
   There is **no connection string** in this configuration.
4. Splits the project into virtual `Subpackages` (`RhetosProjectContentProvider.SplitProjectToSubpackages`; v5.4+).
5. Registers an assembly resolver over `Assemblies`, and warns about legacy packages that have DLLs in `Plugins/`.
6. Scans the plugins (MEF exports in `Assemblies`, cached in `PluginScanner.json`) and builds an Autofac container from `CoreModule`,
   `CorePluginsModule`, `BuildModule` and the plugin modules. Fails if no `ISqlUtility` (DB provider) is registered.
7. `ApplicationGenerator.ExecuteGenerators`: empties `Assets/`, then runs all `IGenerator` plugins as a parallel dependency graph.
   `DslSyntaxFileGenerator` and `DslDocumentationFileGenerator` start first, "to make sure that DSL IntelliSense will work even if there are
   syntax errors in DSL script" (comment in `ApplicationGenerator.cs`). Core generators are registered in
   `src/Rhetos.Core/Configuration.Autofac/Modules/BuildModule.cs`: DslSyntax, DslDocumentation, Dom (C# source), DatabaseModel,
   DataMigrationScripts, ConceptDataMigration, InstalledPackages, Resources. Plugins can add more, e.g. EF6 mapping in `Rhetos.MsSqlEf6`.
8. DSL parsing is lazy (when a generator first needs `IDslModel`): `DiskDslScriptLoader` → `Tokenizer` → `DslParser` → macro evaluation →
   `DslModel.json`. Script selection (`src/Rhetos.Core/Dsl/DiskDslScriptLoader.cs`): extension `.rhe`; inside a package the file must be
   under `DslScripts\`; in the current project any path under the project folder is accepted; script name = `<PackageId>\<subpath>`.
9. Deletes obsolete generated sources and writes `Rhetos.ExternalSourceItems`.

### 2.3 What `rhetos dbupdate` does (brief)

The argument is the app's startup assembly in v5+ (`rhetos dbupdate <App>.dll`) and the application folder in v4
(`git:v4.3.0:Source/RhetosCli/Program.cs`). `Wiki/Rhetos-CLI.md` still documents the v4 form. In v5+, the CLI restarts itself with
`dotnet exec --runtimeconfig <App>.runtimeconfig.json --depsfile <App>.deps.json`. It then creates the app's host through
`Program.CreateHostBuilder` or the minimal hosting model (`src/Rhetos.Core/Configuration.Autofac/HostResolver.cs`), and adds
`rhetos-dbupdate.settings.json` and the CLI switches (`--short-transactions`, `--skip-recompute`). Next, `DatabaseDeployment.UpdateDatabase`
validates the connection, runs the data-migration scripts, and updates the database structure from `DatabaseModel.json`
(`src/Rhetos.Core/Deployment/DatabaseDeployment.cs`). Finally, `ApplicationInitialization` runs the `IServerInitializer` plugins.

### 2.4 Database requirement per step

| Step | Needs a database? |
|---|---|
| NuGet restore, `ResolveRhetosProjectAssets`, `rhetos build`, `CoreCompile` | **No**. The build configuration has no connection string (verified). The DB provider package is still required for the SQL dialect |
| `DeployRhetosApp` / `rhetos dbupdate` (+ app initialization) | **Yes** (`ValidateDbConnection`) |

An editor can therefore regenerate `DslSyntax.json` with `dotnet build -p:RhetosDeploy=false` and no database *(inferred from the conditions above)*.

## 3. Plugin packages

| Package content (nupkg folder) | Consumed by | Listed in assets file as |
|---|---|---|
| `lib/<tfm>/*.dll` (+ `.xml` docs) | Plugin scanner (concepts, macros, code generators, DB generators); XML docs → `DslDocumentation.json` | `Assemblies[]` + `ContentFiles` |
| `DslScripts/**/*.rhe` | `DiskDslScriptLoader` | `ContentFiles` with `InPackagePath` `DslScripts\…` |
| `DataMigration/**/*.sql` | `DataMigrationScriptsGenerator` | `ContentFiles` |
| `Resources/**` | `ResourcesGenerator` (only with `BuildResourcesFolder`) | `ContentFiles` |
| `build/`, `buildTransitive/` `.props`/`.targets` | MSBuild, e.g. `src/Rhetos.MsSql/Rhetos.MsSql.props` defines `RHETOS_MSSQL` and LINQPad references | (not used by Rhetos) |
| `Plugins/*.dll` (DeployPackages era) | Not loaded; a warning is logged (`ApplicationBuild.ReportLegacyPluginsFolders`) | `ContentFiles` |

Example: `src/Rhetos.CommonConcepts.nuspec` packs `DslScripts/**`, `DataMigration/**` and `lib/net10.0/Rhetos.CommonConcepts.dll`+`.xml`.
`Wiki/Creating-a-Rhetos-package.md` describes the same folder convention (its sample nuspec is old, `lib\net472`).
Other plugin packages ship scripts too: in the v6 Bookstore assets file, `Rhetos.Impersonation` 6.0.0 has `DslScripts\Impersonation.rhe`. The package id can
differ from the DLL name: `Rhetos.RestGenerator` 6.0.0 ships `Rhetos.Host.AspNet.RestApi.dll`.

**`Rhetos.LanguageServicesCompatibility`** (`LSC/Readme.md`, `LSC/src/Rhetos.LanguageServicesCompatibility.nuspec`): a `net472` plugin for
Rhetos `[4.0.0,5.0.0)`. It registers its own `DslSyntaxFileGenerator` and `DslDocumentationFileGenerator` (`LSC/src/…/AutofacModuleConfiguration.cs`),
so a Rhetos 4 build writes `obj/Rhetos/DslSyntax.json` and `DslDocumentation.json` with DSL syntax `Version = 5.0`
("compatible with v5.0", `LSC/src/…/Rhetos.Dsl.Parser/DslSyntax.cs`). `LS/README.md` states that v4 apps need this package and v5+ apps do not.
The Rhetos 4 revision of the Bookstore (branch `rhetos-4`, `ae4d5ae`, 2022-03-25, "Added Rhetos.LanguageServicesCompatibility for new DSL IntelliSense
support in Visual Studio"; examined at `afa98a5`, one commit later) references it (1.0.0, with `Rhetos.CommonConcepts` 4.3.0), and
`Bookstore v4/src/Bookstore.Service/obj/Rhetos` contains both files (`DslSyntax.json`: `Version` 5.0, `RhetosVersion` 4.3.0). The current Bookstore
(`master`, Rhetos 6.0.0) does not reference the package; Rhetos writes both files itself (`Version` 6.0).

## 4. Version matrix

| Aspect | Rhetos 4 (4.0–4.3, 2020–21) | Rhetos 5 (5.0–5.6, 2022–26) | Rhetos 6.0 (2025-09) | 6.1.1-dev (unreleased) |
|---|---|---|---|---|
| App / library TFM | .NET Framework 4.7.2 (`lib\net472`); most libs netstandard2.0 | 5.0: net5.0; 5.1+: net5.0 + net6.0 | net8.0 | net10.0 (`ChangeLog.md` 6.1.1) |
| Typical host | WCF web app (`Rhetos.Wcf`) | any .NET app, ASP.NET Core | same | same |
| Build mechanism | `Rhetos.MSBuild` + CLI, **or** legacy `DeployPackages.exe` | CLI only (DeployPackages removed, `ChangeLog.md` 5.0.0) | CLI | CLI |
| CLI build invocation | `"tools\rhetos.exe" build "<dir>" --msbuild-format`, `BeforeTargets="CoreCompile"` (`git:v4.3.0:Source/MsBuildIntegration/Rhetos.MSBuild.targets`) | `dotnet tools\rhetos.dll … build`, `BeforeTargets="BeforeCompile"`; tools net5.0 (5.0), net6.0 (5.1+) | same; tools net8.0, which need the `Microsoft.NETCore.App` **and** `Microsoft.AspNetCore.App` 8.0 runtimes (`tools/rhetos.runtimeconfig.json` of the 6.0.0 package, no `rollForward`) | tools net10.0 (same two frameworks; `FrameworkReference Microsoft.AspNetCore.App` in `src/RhetosCli/RhetosCli.csproj`) |
| dbupdate target / argument | in `Rhetos.MSBuild`; `rhetos.exe dbupdate <bin folder>` | in `Rhetos.Host`; `rhetos.dll dbupdate <App>.dll` | same | same |
| `.rhe` location in project | `DslScripts\**` only (`git:v4.3.0:…/Rhetos.MSBuild.props`) | `DslScripts\**` until 5.2; anywhere from 5.3 | anywhere | anywhere |
| `DslSyntax.json` / `DslDocumentation.json` | not in Rhetos (`git:v4.3.0` has no `DslSyntaxFile`); only via `Rhetos.LanguageServicesCompatibility` (Version 5.0) | generated, `Version` 5.0 | generated, `Version` 6.0 | 6.0 |
| `rhetos-project.assets.json` | `obj/Rhetos/`, with `RhetosTargetEnvironment` | `obj/Rhetos/`, without it | same | same |
| `Rhetos.ExternalSourceItems` | no | no | yes | yes |
| ORM / database | EF6, SQL Server; provider built in | EF6; SQL Server / Oracle | `Rhetos.MsSqlEf6` (EF6 6.5.1, fully supported; used by the Bookstore) or `Rhetos.MsSql` (EF Core, not yet supported by all plugins); PostgreSQL in development; provider package **required** | EF6 6.5.2 / EF Core 10 |
| Runtime config | Web.config + `rhetos-app(.local).settings.json` | `appsettings.json`, host-defined | same | same |
| Core DLLs | many `Rhetos.*.dll` | many (`Rhetos.Dsl.Parser.dll`, `Rhetos.Dsl.Interfaces.dll`, …) | consolidated: `Rhetos.Core`, `.Core.DslParser`, `.Core.Integration`, `.CommonConcepts` (`ChangeLog.md` 6.0.0) | same |
| DSL features affecting the editor | – | block comments `/* */` (5.4), Load/Query/Filter concepts (5.3), Subpackages (5.4) | SQL resource references in DSL | – |
| Bookstore sample (github.com/Rhetos/Bookstore) | branch `rhetos-4` (4.3.0, net472, WCF, with `Rhetos.LanguageServicesCompatibility`) | branch `rhetos-5` (5.6.0, net5.0, updated 2026-02-03) | `master` (6.0.0, net8.0, EF6; `aa024a0`, 2025-09-23) | – |

Rhetos ≤3 uses DeployPackages only (`RhetosPackages.config`, `bin\Generated`, `bin\Plugins`; `Wiki/Installing-plugin-packages.md`,
`Wiki/Migrating-from-DeployPackages-to-Rhetos-CLI.md`) and has no `obj/Rhetos`. Language Services cannot work with these versions *(inferred)*.

## 5. Packages and assemblies (working tree, `src/*.nuspec`)

| NuGet package | Ships | TFM | Purpose |
|---|---|---|---|
| `Rhetos` | `Rhetos.Core.dll` | net10.0 | Framework core: DSL model, code generation, DB generator, plugin scanning, runtime, host (`Rhetos.Host.Net` merged here in 6.0) |
| `Rhetos.DslParser` | `Rhetos.Core.DslParser.dll`, `Rhetos.Core.Integration.dll` | netstandard2.1 | Tokenizer, parser, `DslSyntax`/`DslSyntaxFile`, `DslDocumentationFile`; build-integration model (`RhetosBuildEnvironment`, `RhetosProjectAssets`, `RhetosProjectContentProvider`). **Referenced by LS** (`LS/src/CodeAnalysis/*.csproj`: `Rhetos.DslParser` 6.0.0) |
| `Rhetos.MSBuild` | `build/` targets + `RhetosVSIntegration.dll` (netstandard2.0 MSBuild task); `tools/` Rhetos CLI | – | Runs `rhetos build` during `dotnet build` |
| `Rhetos.Host` | `build/` dbupdate targets; `rhetos.dll` copied to `bin` | – | Runs `rhetos dbupdate` after the build; deploys the CLI with the app |
| `Rhetos.CommonConcepts` | `Rhetos.CommonConcepts.dll`, `DslScripts/`, `DataMigration/` | net10.0 | Standard DSL concept library |
| `Rhetos.EfCore` | `Rhetos.EfCore.dll` | net10.0 | EF Core support |
| `Rhetos.MsSql` / `Rhetos.MsSqlEf6` / `Rhetos.PostgreSql` | provider DLL + `buildTransitive/*.props` | net10.0 | DB provider: SQL Server + EF Core / SQL Server + EF6 (legacy) / PostgreSQL + EF Core. `src/Rhetos.MsSql.Shared` is a shared source project used by both SQL Server providers |
| `Rhetos.Host.AspNet` | `Rhetos.Host.AspNet.dll` | net10.0 | ASP.NET Core integration (dashboard, etc.) |
| `Rhetos.TestCommon` | `Rhetos.TestCommon.dll` | net10.0 | MSTest helpers |

## 6. Relevant for Language Services

**Detection.** The current LS code treats a folder as a Rhetos app root if `<folder>/obj/Rhetos/DslSyntax.json` exists
(`LS/src/CodeAnalysis/Tools/DslSyntaxProvider.cs` `IsValidProjectRootPath`). `LS/README.md` (Usage, rule 3) was corrected on 2026-10-02 and now names
`obj/Rhetos/DslSyntax.json`. The earlier text (assets file) dated from LS v1.0.0, which loaded the plugin DLLs listed in the assets file
(`git:v1.0.0:src/Rhetos.LanguageServices.Server/Services/RhetosAppContext.cs` in the LS repo). Recommended rules for a VS Code client:
(a) Rhetos app = folder with a `.csproj` whose restored packages import the `Rhetos.MSBuild` build targets (or that contains `obj/Rhetos/rhetos-project.assets.json`).
A direct `PackageReference` is not required: the v4 Bookstore gets `Rhetos.MSBuild` 4.3.0 only through `Rhetos.Wcf`, and its `obj/Bookstore.Service.csproj.nuget.g.targets`
imports `Rhetos.MSBuild.targets`. A project that only references the app (`ProjectReference`) receives `Rhetos.MSBuild` without build assets (`"build/_._"` in its
`obj/project.assets.json`) and is not a Rhetos app: the v6 Bookstore test and playground projects have no `obj/Rhetos`;
(b) "ready" = `obj/Rhetos/DslSyntax.json` exists. If only (a) holds, the editor should report "build the project first". A design-time build writes
the assets file but never `DslSyntax.json`, because `BuildRhetosApp` skips `DesignTimeBuild`.

| Watch | Why |
|---|---|
| `obj/Rhetos/DslSyntax.json` | Concept syntax; rewritten only when the content changes, so a timestamp change means new syntax (the current LS compares `LastWriteTime`) |
| `obj/Rhetos/DslDocumentation.json` | Hover and completion documentation (optional) |
| `obj/Rhetos/rhetos-project.assets.json` | Touched on every build; gives the package list, which `.rhe` files belong to the app, and the read-only package scripts in the NuGet cache. It starts with a UTF-8 BOM, which a Node.js client must strip before `JSON.parse` *(inferred)*. `ContentFiles` can name files that no longer exist (a NuGet cache folder deleted after the build, a deleted `.rhe` file; both observed in a local build of the v6 Bookstore), so a client must tolerate missing files |
| `**/*.rhe`, `rhetos-build.settings.json` | Sources and build options (`Rhetos:Build:DslSyntaxExcessDotInKey` is also persisted in `DslSyntax.json`) |

**Version-specific differences that matter for an editor:**
- `DslSyntax.Version` is 5.0 (v5, and v4 with the compatibility package) or 6.0 (v6). `DslSyntaxFile.Load` warns when the app's version is greater
  than the `CurrentVersion` of the referenced `Rhetos.DslParser` (6.0 in 6.0.0 and in the working tree).
- `DslSyntax.json` also carries `DatabaseLanguage` (e.g. `MsSql`, `PostgreSql`) and `ExcessDotInKey`
  (`src/Rhetos.Core.DslParser/DslSyntax.cs`); both affect parsing.
- v4 apps: `DslSyntax.json` exists only if `Rhetos.LanguageServicesCompatibility` is installed, and `.rhe` files must be in `DslScripts\`.
  v5.0–5.2 apps also require `DslScripts\`. Block comments are valid only on v5.4+ (`LS/CHANGELOG.md` 2.2.0).
- Rhetos hard-codes `obj` (`NuGetUtilities.cs`, `RhetosProjectContentProvider.cs`, `obj\Rhetos\Source` in the targets). Only `CacheFolder`
  follows `$(BaseIntermediateOutputPath)`, so a custom intermediate path is effectively unsupported *(inferred)*.
- `.rhe` files outside the project folder are compiled only through `Subpackages`. Files in referenced projects are not compiled (section 1.3).
  An editor should not assume that every `.rhe` file under the workspace is part of the app; the `ContentFiles` list in the assets file is
  the authoritative list.

**Open items / unverified:** (1) `RhetosTargetFramework`, named in the task, does not exist in the working tree (grep found nothing).
(2) LS support for v4 apps that still use DeployPackages instead of Rhetos.MSBuild is not verified. (3) `Wiki/Creating-a-new-application-with-Rhetos-framework.md`
omits the DB provider package that v6 requires, `Wiki/Rhetos-CLI.md` shows the v4 `dbupdate` argument, and `Wiki/Build-binaries-from-source.md`
says `Install` where the repo builds into `dist`. (4) `ChangeLog.md` 5.6.0 refers to a "6.1.0" release, but the next heading is "6.1.1 (TO BE RELEASED)".
