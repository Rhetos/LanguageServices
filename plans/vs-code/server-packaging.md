# Server packaging, runtime, versioning, release and CI

Back to [README.md](README.md).

## 1. Publish the server

The VSIX bundles a framework-dependent, portable publish of `src/Server`:

```
dotnet publish src/Server/Server.csproj -c Release -o src/VSCodeExtension/server -p:UseAppHost=false
```

| Property | Value | Why |
| --- | --- | --- |
| Framework-dependent | yes | Small (roughly 5-8 MB: OmniSharp LSP, MediatR, Newtonsoft.Json, NLog, Rhetos.DslParser). One VSIX for all platforms |
| `UseAppHost=false` | for the VSIX | No `.exe`; the client always runs `dotnet Rhetos.LanguageServices.Server.dll`. The MSI/VS extension still needs the `.exe`, so the default build keeps the apphost and only the VS Code publish disables it |
| `NLog.config` | included | It is `CopyToOutputDirectory=PreserveNewest`, so publish includes it. Prerequisite A5 changes its default log path to a per-user location |
| `RollForward` | `LatestMajor` (prerequisite A1) | Lets the server run on .NET 9/10 when only those are installed. Framework-dependent apps do not roll forward across major versions by default |
| PDB and XML doc files | excluded via `.vscodeignore` | Size |

Decision that stays open until prerequisite A2 is done: whether the publish output is produced once into `dist/server/` and consumed by both the MSI (vdproj currently references `..\Server\obj\Debug\net8.0\apphost.exe` plus the `PublishItems` output group) and the VSIX, or whether each client runs its own publish. One shared `dist/server/` is simpler to reason about and to sign.

## 2. VSIX variants

| Variant | When |
| --- | --- |
| Universal VSIX (framework-dependent server) | Default. `vsce package` without `--target` |
| Platform-specific VSIX with self-contained server (`vsce package --target win32-x64`, `linux-x64`, `darwin-arm64`, ...) | Only if runtime acquisition turns out to be a support burden. Roughly 70 MB each. Can be added later without breaking users because VS Code picks the platform build automatically |

## 3. .NET runtime on the user machine

Order used by the client (see `dotnetRuntime.ts` in [extension-structure.md](extension-structure.md)):

1. Setting `rhetos.dotnetPath`.
2. `.NET Install Tool` extension (`ms-dotnettools.vscode-dotnet-runtime`), command `dotnet.acquire` with the server's major version. Declared as `extensionDependencies` so it is installed automatically.
3. `dotnet` on PATH (works with any newer major thanks to `RollForward=LatestMajor`).

Error UX when all fail: modal error with buttons "Open settings" and "Help" (link to the README troubleshooting section). The Output channel shows the attempted paths.

## 4. Versioning

Current scheme: `Directory.Build.props` holds `AssemblyVersion`/`FileVersion` (`6.1.0`) and `InformationalVersion` (`6.1.0-dev`), `tools/ChangeVersion.ps1` rewrites them together with the VSIX manifests and the vdproj.

Additions:

| File | Field | Rule |
| --- | --- | --- |
| `src/VSCodeExtension/package.json` | `"version"` | Always `major.minor.patch` without suffix. `ChangeVersion.ps1` gets one more `RegexReplace` for `("version"\s*:\s*").*?(")` restricted to that file |
| Marketplace pre-release | `vsce publish --pre-release` | Marketplace rejects semver suffixes. Internal builds (`ChangeVersion.ps1 x.y.z auto`) are not published; if a public preview is wanted, publish the same `x.y.z` with `--pre-release`. Marketplace convention is odd minor numbers for pre-release channels; adopting it would couple the shared version to VS Code rules, so prefer "no public pre-releases" for now |
| `server/server-info.json` (new, written by publish or by an MSBuild target) | `version`, `targetFramework`, `commit` | Lets the client log and validate what it bundles and pick the runtime major version without hardcoding |

The server should also answer `--version` on the command line (prerequisite A6) for troubleshooting.

## 5. Release checklist (extends README "Releasing a new version")

1. `powershell .\tools\ChangeVersion.ps1 x.y.z` and update `CHANGELOG.md`.
2. Build and test the server headlessly: `dotnet build` and `dotnet test` (prerequisite A1), then the Visual Studio solution for the MSI as today.
3. `cd src/VSCodeExtension && npm ci && npm run package` -> `rhetos-dsl-x.y.z.vsix`.
4. Smoke test: `code --install-extension rhetos-dsl-x.y.z.vsix` on a clean profile (`code --profile-temp`), open a `.rhe` in a built Rhetos app.
5. Tag, GitHub release with assets: `RhetosLanguageServicesInstaller.msi` and `rhetos-dsl-x.y.z.vsix`.
6. `npx vsce publish --packagePath rhetos-dsl-x.y.z.vsix` (needs `VSCE_PAT`, a Marketplace PAT with "Marketplace > Manage" scope for the `rhetos` publisher). Optional: `npx ovsx publish` for Open VSX.
7. `ChangeVersion.ps1 x.(y+1).0 dev`, commit "Development x.(y+1).0.".

## 6. CI (GitHub Actions)

`.github/workflows/` is currently empty. Proposed `build.yml` on `windows-latest` (the server is cross-platform, but Windows keeps the option of also building the VS solution later):

```yaml
on: [push, pull_request, workflow_dispatch]
jobs:
  server:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }          # or the version matching Server.csproj
      - run: dotnet build Rhetos.LanguageServices.Server.slnf -c Release   # solution filter from prerequisite A1
      - run: dotnet test test/CodeAnalysis.Test -c Release --no-build
      - run: dotnet test test/LspIntegration.Test -c Release --no-build
      - run: dotnet publish src/Server/Server.csproj -c Release -o dist/server -p:UseAppHost=false
      - uses: actions/upload-artifact@v4
        with: { name: server, path: dist/server }
  vscode:
    needs: server
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/download-artifact@v4
        with: { name: server, path: src/VSCodeExtension/server }
      - uses: actions/setup-node@v4
        with: { node-version: 20, cache: npm, cache-dependency-path: src/VSCodeExtension/package-lock.json }
      - run: npm ci
        working-directory: src/VSCodeExtension
      - run: npm run compile && npx vsce package
        working-directory: src/VSCodeExtension
      - run: xvfb-run -a npm test     # on ubuntu; on windows just `npm test`
        working-directory: src/VSCodeExtension
      - uses: actions/upload-artifact@v4
        with: { name: vsix, path: src/VSCodeExtension/*.vsix }
```

A second workflow `release.yml` triggered by tags `v*` downloads the artifacts, creates the GitHub release and runs `vsce publish` with the `VSCE_PAT` secret. The MSI stays a manual step until the vdproj is replaced (prerequisites plan B1).

## 7. Licensing and notices

The repository is AGPL-3.0 (`License.txt`). The VSIX must include `LICENSE`. Bundled third-party binaries (OmniSharp.Extensions, MediatR, Newtonsoft.Json, NLog, Rhetos.DslParser, Microsoft.Extensions.*) are MIT/Apache; add a `THIRD-PARTY-NOTICES.txt` to the extension generated from the publish output (for example with `nuget-license` or a maintained static file).
