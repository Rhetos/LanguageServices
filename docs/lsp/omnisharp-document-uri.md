# OmniSharp `DocumentUri` behaviour (LSP document URIs)

Reference notes for `OmniSharp.Extensions.LanguageServer` 0.19.9, the LSP library used by `src/Server`.
They record what was measured on 2026-10-02 while making the server publish diagnostics with the URI of the opened document (URI round-trip), because the NuGet package ships no XML documentation
(`lib/` holds only the dll and pdb) and the behaviour is not derivable from this repository.
Measured on Windows 11 with the .NET 10 runtime, by reflection probes and a console experiment against the 0.19.9 assemblies in the test output,
and by raw JSON-RPC sessions against the built server. Where a statement comes from the OmniSharp source or from general knowledge instead, the text says so.

Related: [language-services-integration.md](../rhetos/language-services-integration.md) (how the server uses the URI),
`src/Server/Tools/DocumentKeyExtensions.cs` (the conversion the server applies),
`test/LspIntegration.Test/DocumentUriWireTests.cs` (a raw JSON-RPC client that drives the server process).

## 1. What `DocumentUri` stores

| Fact | Detail |
| --- | --- |
| Storage | Only the parts `Scheme`, `Authority`, `Path`, `Query`, `Fragment`. The original string is not kept. A `%3A` in the path is decoded to `:` in `Path`. |
| Wire format | The type carries `[JsonConverter(typeof(DocumentUriConverter))]`. The converter writes `DocumentUri.ToString()`. Every incoming `uri` is parsed into parts while the JSON message is read, so a handler never sees the client's string. |
| Extensibility | The class is not sealed and `ToString()` is virtual. Its only constructor takes the parts. A byte-exact echo of the client's string would need a subclass that stores the original string plus raw-JSON interception on input; this project does not do that. |
| Comparer | `DocumentUri.Equals` uses `OrdinalIgnoreCase` on all parts, except on Linux (from the 0.19.2 source; measured true on 0.19.9). A static `DocumentUri.Comparer` exists. Note that this also makes `untitled:Untitled-1` equal to `untitled:untitled-1`, which `System.Uri` does not. |

## 2. How `ToString()` renders URIs

Rules for Windows file URIs: drive letter lowercased, the colon after the drive letter unencoded, path case kept, spaces and non-ASCII characters percent-encoded.

| Input (as parsed from JSON or by `DocumentUri.Parse`) | `ToString()` (what goes on the wire) | `ToUri()` and its `LocalPath` |
| --- | --- | --- |
| `file:///c%3A/Temp/x.rhe` (VS Code form) | `file:///c:/Temp/x.rhe` | `file:///c:/Temp/x.rhe`, `c:\Temp\x.rhe` |
| `file:///C:/Temp/x.rhe` (Visual Studio form, see §6) | `file:///c:/Temp/x.rhe` | `file:///C:/Temp/x.rhe`, `C:\Temp\x.rhe` (drive case kept) |
| `file:///c%3A/Temp/Dir%20With%20Space/%C4%8D.rhe` | `file:///c:/Temp/Dir%20With%20Space/%C4%8D.rhe` | LocalPath decoded: `c:\Temp\Dir With Space\č.rhe` |
| `file:///C:/Temp/Dir With Space/raw space.rhe` (invalid, not encoded) | `file:///c:/Temp/Dir%20With%20Space/raw%20space.rhe` | decoded path |
| `file:///c:/a.rhe?q=1#frag` | `file:///c:/a.rhe?q%3D1#frag` (query re-encoded) | `c:\a.rhe` |
| `FILE:///C:/x.rhe` | `FILE:/c:/x.rhe` (quirk: the scheme case is kept and the slashes change) | |
| `file://server/share/x.rhe` (UNC) | unchanged | `\\server\share\x.rhe`; `Path.IsPathFullyQualified` is true |
| `file:///home/x` (POSIX path, on Windows) | unchanged | `/home/x`; not fully qualified on Windows |
| `untitled:Untitled-1` | unchanged | `IsFile` false; `LocalPath` is `Untitled-1`, a relative path |
| `git:`, `vscode-notebook-cell:` URIs | unchanged | `ToUri().LocalPath` can still yield a path such as `c:\x.rhe`; the server treats non-`file` schemes as non-file documents |

Consequence: a `DocumentUri` that went through `System.Uri` and back (`DocumentUri.From(documentUri.ToUri())`) renders exactly the same string as the directly parsed one, for every probed input
(mixed drive case, spaces, non-ASCII, `#`, `%25`, `+`, `&`, `[]`, `~`, `;`, `@`, `?`, UNC, `untitled:`). This is why the 2022 `path-case` branch, which replaced `System.Uri` with `DocumentUri` throughout, produces no observable difference on the wire with these packages.

## 3. Conversions and their traps

| Member | Behaviour | Use it for |
| --- | --- | --- |
| `DocumentUri.Parse(string)` | Parses a URI string into parts. `Parse(x.ToString()).ToString() == x.ToString()`. | Rebuilding a `DocumentUri` from a stored canonical string (`PublishDiagnosticsRunner`). |
| `DocumentUri.From(string)` | Treats strings that start with `/` or `\\`, or look like Windows paths, as file-system paths. | Not for stored URI strings. |
| `DocumentUri.From(System.Uri)` (also the implicit `Uri` → `DocumentUri` operator) | If `OriginalString` contains `%3A`: `From(OriginalString)`. Else if the scheme is `file`: `From(uri.LocalPath)`. Else `From(uri.ToString())`, and `Uri.ToString()` unescapes, which is risky for non-file schemes. | Avoid; the implicit operator makes these conversions easy to miss. |
| `DocumentUri.From(@"C:\Temp\x y.rhe")`, `FromFileSystemPath` | `file:///c:/Temp/x%20y.rhe` | Building a URI from a path, for example in tests. |
| `ToUri()` | Rebuilds a `System.Uri` with `UriBuilder` from the decoded parts (`Scheme`, `Host = Authority`, `Path`). `%3A` disappears and the drive letter case is kept. Can throw `UriFormatException`, `ArgumentException` or `InvalidOperationException` for malformed input. | Getting `LocalPath` for `file` URIs. |
| `GetFileSystemPath()` | Always lowercases the drive letter (`c:\...`). | Not used by the server, because root-path comparisons in `RhetosWorkspace` are case-sensitive string comparisons and tests compare against `Environment.CurrentDirectory`. |
| `new System.Uri("file:///c%3A/Temp/X.rhe")` | Is **not** a DOS-path URI. `LocalPath` is `/c:/Temp/X.rhe`, and it does not equal `new Uri("file:///C:/Temp/X.rhe")`. | Never derive a local path from the raw string with `System.Uri`. |

`System.Uri` equality, for comparison: DOS-path file URIs compare case-insensitively on the whole URI including the path; `http` paths and `untitled:` compare case-sensitively.

## 4. What the server does with this (since version 6.1.0)

| Step | Behaviour |
| --- | --- |
| Receiving | `DocumentKeyExtensions.ToDocumentKey` sets the key's URI to `documentUri.ToString()` and, for the `file` scheme only, `LocalPath = documentUri.ToUri().LocalPath`. If `ToUri()` throws or the path is not fully qualified, the document counts as a non-file document (Debug log). |
| Keying | `RhetosWorkspace` keys documents by the URI string with `OrdinalIgnoreCase` (ordinal on Linux), mirroring `DocumentUri.Equals`. The key from the first `didOpen` is kept, so the published URI always equals the opened URI. |
| Publishing | `DocumentUri.Parse(storedString)`; no `System.Uri` in between. |
| Non-file documents | No root path, exactly one warning diagnostic, and no resolution of a relative `rhetosProjectRootPath` directive against the process working directory (an absolute directive is honoured). Before version 6.1.0 a fake application in the server's working directory was detected for an `untitled:` document; this was confirmed live. |
| `DslScript.Path` | `LocalPath ?? Uri`, never empty (see §5). |

## 5. Rhetos facts that interact with URIs

Source: local Rhetos6 checkout (`src/Rhetos.Core.DslParser/DslSyntaxException.cs`, `DslParser.cs`), version 6.1.1-dev; the server uses `Rhetos.DslParser` 6.0.0.

| Fact | Consequence |
| --- | --- |
| The six-argument `DslSyntaxException` constructor sets `FilePosition` only when `dslScript.Path` is not empty. | `DslScript.Path` must never be empty, otherwise `CodeAnalysisRun.CreateAnalysisError` dereferences a null `FilePosition`. |
| The one-argument `DslSyntaxException(string)` never sets `FilePosition`. `DslParser` throws it for a warning promoted to an error when `ExcessDotInKey == Error`. | Pre-existing crash path independent of URIs: the `NullReferenceException` is thrown inside the `catch (DslSyntaxException)` block in `CodeAnalysisRun`, so the sibling `catch (Exception)` does not catch it; `PublishDiagnosticsRunner` logs a warning each 300 ms cycle and no document gets diagnostics. Not fixed yet (2026-10-02). |
| Rhetos does no file-system operations on `DslScript.Path`; `SimpleExternalResourceReader` in this project reads no files. | A URI string as the path for non-file documents is safe. |

## 6. Client behaviour (not measured here)

| Client | Behaviour | Status |
| --- | --- | --- |
| VS Code | `vscode-uri` `URI.toString()` renders `file:///c%3A/...` with a lowercase drive letter, and the client compares URIs after `URI.parse`, under which `file:///c:/x` and `file:///c%3A/x` are the same resource. A VS Code client for this server should restrict its `documentSelector` to `scheme: 'file'`, because non-file documents get only a warning diagnostic (§4). | General knowledge of `vscode-uri`; not tested in this repository. |
| Visual Studio 2022 | Believed to send `file:///C:/...` (uppercase drive, unencoded colon). The repository cannot show it: `src/VisualStudioExtensionShared/LanguageClient.cs` has no URI code, the built-in VS LSP client builds the URI. The 2022 `path-case` commit message describes a casing difference between received and published URIs, which fits this form. | Inferred; unverified. |

## 7. Testing notes

| Topic | Note |
| --- | --- |
| In-process test client | `LanguageProtocolTestBase` (package `OmniSharp.Extensions.LanguageProtocol.Testing`) serializes through `DocumentUri` too. It cannot send `file:///c%3A/...` on the wire, and the received `PublishDiagnosticsParams.Uri` is already normalized. Tests in `DocumentUriTests.cs` therefore assert `published.Uri.ToString() == opened.Uri.ToString()`. |
| Wire-level test | `DocumentUriWireTests.cs` starts `dotnet Rhetos.LanguageServices.Server.dll` from the test output folder, speaks Content-Length-framed JSON-RPC over stdin/stdout, drains stderr, and asserts the raw published `uri` string. It is marked inconclusive when `dotnet` is not found. |
| Minimal `initialize` | The server throws `NullReferenceException` in `RhetosLanguageServer.OnInitializedAsync` when the client sends no `textDocument.synchronization` capability (`TextDocumentSync.Options` is null). Raw clients must send at least `capabilities: { textDocument: { synchronization: { dynamicRegistration: false } } }`. Not fixed yet (2026-10-02). |
| Server log during tests | The server reads `NLog.config` from its folder, so the wire test writes to `C:\ProgramData\Rhetos.LanguageServices\Rhetos.LanguageServices.Server.log`. The log path is fixed in `src/Server/NLog.config`; `C:\ProgramData` is not writable everywhere and does not exist on Linux or macOS. |

## 8. Sources

| Source | What it was used for |
| --- | --- |
| OmniSharp `csharp-language-server-protocol`, tag `v0.19.2`, `src/Protocol/DocumentUri.cs` and `DocumentUri.Internal.cs` (GitHub, fetched 2026-10-02) | `From`, `File`, `ToUri`, `Parse` logic and the comparer rules. The 0.19.9 assemblies behaved the same in every probe. |
| Reflection probes and a console experiment against the 0.19.9 assemblies in `test/LspIntegration.Test/bin` | The rendering and equality tables above. |
| Raw JSON-RPC sessions against the built server | The published URI forms and the `untitled:` working-directory behaviour before version 6.1.0. |
| Local Rhetos6 checkout (see `docs/rhetos/README.md`, Sources) | The `FilePosition` rule in §5. |
