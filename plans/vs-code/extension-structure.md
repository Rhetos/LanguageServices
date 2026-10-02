# VS Code extension: structure and code sketch

Back to [README.md](README.md).

## 1. Folder layout

```
src/VSCodeExtension/
  package.json                 extension manifest (contributions, scripts, dependencies)
  package-lock.json
  tsconfig.json
  esbuild.mjs                  bundles src/ into dist/extension.js (single file, no node_modules shipped)
  .vscodeignore                excludes src/, node_modules/, tests from the VSIX
  .eslintrc.json (optional)
  README.md                    Marketplace page: features, requirements, settings, troubleshooting
  CHANGELOG.md                 generated/copied from repository CHANGELOG.md at build time, or a short pointer
  LICENSE                      copy of ../../License.txt (AGPL-3.0)
  icon.png                     128x128
  language-configuration.json  comments, brackets, auto-closing pairs
  syntaxes/
    rhetos.tmLanguage.json     COPIED at build from ../VisualStudioExtensionSharedFiles/Grammars/Rhetos.json (gitignored)
  server/                      COPIED at build from dotnet publish output (gitignored)
  src/
    extension.ts               activate/deactivate, command registration
    client.ts                  LanguageClient creation and restart
    serverLocator.ts           finds server dll: setting -> bundled
    dotnetRuntime.ts           resolves dotnet executable: .NET Install Tool -> setting -> PATH
    log.ts                     Output channel helpers
  test/
    runTest.ts                 @vscode/test-electron launcher
    suite/*.test.ts            Mocha tests
    fixtures/                  points at ../../test/RhetosMockAppContents
```

The `.vscode/launch.json` at the repository root (or inside `src/VSCodeExtension/`) gets an "Run Extension" configuration and a "Extension Tests" configuration.

## 2. `package.json` (essential parts)

```jsonc
{
  "name": "rhetos-dsl",
  "displayName": "Rhetos DSL",
  "description": "IntelliSense, diagnostics and syntax highlighting for Rhetos DSL (.rhe) scripts.",
  "publisher": "rhetos",
  "version": "6.1.0",                       // kept in sync by tools/ChangeVersion.ps1
  "license": "AGPL-3.0-only",
  "repository": { "type": "git", "url": "https://github.com/Rhetos/LanguageServices" },
  "engines": { "vscode": "^1.90.0" },
  "categories": ["Programming Languages", "Linters"],
  "keywords": ["rhetos", "dsl", "rhe"],
  "main": "./dist/extension.js",
  "activationEvents": [],                   // implied by contributes.languages in modern VS Code
  "extensionDependencies": ["ms-dotnettools.vscode-dotnet-runtime"],
  "contributes": {
    "languages": [{
      "id": "rhetos-dsl",
      "aliases": ["Rhetos DSL", "rhetos"],
      "extensions": [".rhe"],
      "configuration": "./language-configuration.json",
      "icon": { "light": "./icons/rhe-light.svg", "dark": "./icons/rhe-dark.svg" }   // optional
    }],
    "grammars": [{
      "language": "rhetos-dsl",
      "scopeName": "source.rhetos",
      "path": "./syntaxes/rhetos.tmLanguage.json",
      "embeddedLanguages": { "source.sql": "sql", "source.cs": "csharp" }
    }],
    "configuration": {
      "title": "Rhetos DSL",
      "properties": {
        "rhetos.languageServer.path": {
          "type": "string", "default": "", "scope": "machine-overridable",
          "markdownDescription": "Path to `Rhetos.LanguageServices.Server.dll` (or `.exe`). Leave empty to use the server bundled with the extension. Intended for development and troubleshooting."
        },
        "rhetos.dotnetPath": {
          "type": "string", "default": "", "scope": "machine-overridable",
          "description": "Path to the dotnet executable used to run the language server. Leave empty to resolve it automatically."
        },
        "rhetos.languageServer.logLevel": {
          "type": "string", "enum": ["Trace", "Debug", "Information", "Warning", "Error"], "default": "Information",
          "description": "Minimum level written to the server log file."
        },
        "rhetos.trace.server": {
          "type": "string", "enum": ["off", "messages", "verbose"], "default": "off",
          "description": "Traces the communication between VS Code and the Rhetos language server."
        }
      }
    },
    "commands": [
      { "command": "rhetos.restartLanguageServer", "title": "Rhetos: Restart Language Server" },
      { "command": "rhetos.openServerLog", "title": "Rhetos: Open Language Server Log File" }
    ]
  },
  "scripts": {
    "copy-grammar": "node scripts/copy-grammar.mjs",
    "copy-server": "dotnet publish ../Server/Server.csproj -c Release -o ./server -p:UseAppHost=false",
    "compile": "npm run copy-grammar && node esbuild.mjs",
    "watch": "node esbuild.mjs --watch",
    "package": "npm run copy-grammar && npm run copy-server && vsce package",
    "test": "node ./out/test/runTest.js"
  },
  "dependencies": { "vscode-languageclient": "^9.0.1" },
  "devDependencies": {
    "@types/vscode": "^1.90.0", "@types/node": "^20", "typescript": "^5",
    "esbuild": "^0.2x", "@vscode/vsce": "^3", "@vscode/test-electron": "^2", "mocha": "^10", "@types/mocha": "^10"
  }
}
```

Notes:

* `embeddedLanguages` makes VS Code treat the `source.sql` / `source.cs` regions inside strings with the right language for bracket matching and comment toggling; the grammar itself already includes those scopes.
* `rhetos.trace.server` follows the `vscode-languageclient` convention: the client reads `<section>.trace.server` automatically when the client id is `rhetos`.
* Version strings on the Marketplace must be `major.minor.patch` only. The repository's `-dev...` prerelease suffix cannot be used; see [server-packaging.md](server-packaging.md) section 4.

## 3. `language-configuration.json`

```json
{
  "comments": { "lineComment": "//", "blockComment": ["/*", "*/"] },
  "brackets": [["{", "}"], ["(", ")"]],
  "autoClosingPairs": [
    { "open": "{", "close": "}" },
    { "open": "(", "close": ")" },
    { "open": "\"", "close": "\"", "notIn": ["string"] },
    { "open": "'", "close": "'", "notIn": ["string"] },
    { "open": "/*", "close": " */", "notIn": ["string"] }
  ],
  "surroundingPairs": [["{", "}"], ["(", ")"], ["\"", "\""], ["'", "'"], ["<", ">"]],
  "wordPattern": "[A-Za-z_][A-Za-z0-9_\\.]*"
}
```

`wordPattern` allows dots so that `Module.Entity` references are one word for completion prefix matching; the server already uses `.` as a completion trigger character.

## 4. Client code sketch (`src/client.ts`)

```ts
import * as path from 'path';
import * as vscode from 'vscode';
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient/node';
import { resolveDotnet } from './dotnetRuntime';
import { resolveServerDll } from './serverLocator';

export async function createClient(context: vscode.ExtensionContext, output: vscode.OutputChannel): Promise<LanguageClient> {
  const config = vscode.workspace.getConfiguration('rhetos');
  const serverDll = resolveServerDll(context, config.get<string>('languageServer.path'));
  const dotnet = await resolveDotnet(context, config.get<string>('dotnetPath'));   // may call dotnet.acquire

  const serverOptions: ServerOptions = {
    command: dotnet,
    args: [serverDll, '--log-level', config.get<string>('languageServer.logLevel') ?? 'Information'],
    transport: TransportKind.stdio,
    options: { cwd: path.dirname(serverDll) }   // so NLog.config is found next to the dll
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'rhetos-dsl' }],   // 'file' only: untitled documents have no folder, so they get no Rhetos context
    outputChannel: output,
    traceOutputChannel: output,
    synchronize: {
      // optional: lets the server react faster than its 1 s polling, if a didChangeWatchedFiles handler is added later
      fileEvents: vscode.workspace.createFileSystemWatcher('**/obj/Rhetos/DslSyntax.json')
    },
    initializationOptions: {}   // reserved; the server ignores it today
  };

  return new LanguageClient('rhetos', 'Rhetos DSL Language Server', serverOptions, clientOptions);
}
```

`extension.ts`:

* `activate`: create Output channel, create and `start()` the client, register the two commands, push disposables.
* `rhetos.restartLanguageServer`: `await client.restart()` (vscode-languageclient 9 supports it); recreate the client if the server path setting changed.
* `rhetos.openServerLog`: the server logs `Log file: '<path>'` on initialize; the client parses that line from `window/logMessage` (or, better, the server returns the path in a custom `rhetos/serverInfo` request added in prerequisite A6) and opens it with `vscode.window.showTextDocument`.
* `deactivate`: `client.stop()`.
* Listen to `onDidChangeConfiguration` for `rhetos.languageServer.path`, `rhetos.dotnetPath` and offer a restart.

## 5. `serverLocator.ts`

Resolution order:

1. `rhetos.languageServer.path` if non-empty. Accept both a `.dll` (run through `dotnet`) and an `.exe` (run directly; Windows only). Fail with a clear message if the file does not exist.
2. Bundled: `context.asAbsolutePath('server/Rhetos.LanguageServices.Server.dll')`.

Also validate on startup that `NLog.config` exists next to the dll, otherwise the server has no file logging (it still works).

## 6. `dotnetRuntime.ts`

```ts
export async function resolveDotnet(context: vscode.ExtensionContext, configured?: string): Promise<string> {
  if (configured) return configured;
  try {
    const result = await vscode.commands.executeCommand<{ dotnetPath: string }>(
      'dotnet.acquire', { version: '8.0', requestingExtensionId: context.extension.id });
    if (result?.dotnetPath) return result.dotnetPath;
  } catch (e) { /* log and fall through */ }
  return 'dotnet';   // PATH lookup; the server has RollForward=LatestMajor (prerequisite A1) so a newer runtime also works
}
```

The `.NET Install Tool` extension (`ms-dotnettools.vscode-dotnet-runtime`) downloads a private copy of the runtime into the extension's global storage, shows its own progress UI and handles offline errors. Declaring it in `extensionDependencies` makes VS Code install it automatically with the Rhetos extension. The `version` constant must follow the server's `TargetFramework` and should be read from a single place (for example `server/server-info.json` written by the publish step, prerequisite A6).

## 7. Grammar copy script

`scripts/copy-grammar.mjs` copies `../VisualStudioExtensionSharedFiles/Grammars/Rhetos.json` to `syntaxes/rhetos.tmLanguage.json`. Keep the copy out of git so the VS grammar file remains the single source. If the grammar's `fileTypes` array is cleaned up (`.alpha` looks like a leftover), do it in the source file.

## 8. `.vscodeignore`

```
.vscode/**
src/**
test/**
out/**
node_modules/**
scripts/**
esbuild.mjs
tsconfig.json
**/*.map
server/*.pdb
server/*.xml          # XML documentation files from GenerateDocumentationFile
```

Keep `server/**` (minus pdb/xml) and `syntaxes/**` in the package.
