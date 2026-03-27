# Analyzer Diagnostics Reference

WrapGod ships a Roslyn analyzer (`WrapGod.Analyzers`) that detects direct
usage of third-party types and methods that should be accessed through
generated wrapper interfaces and facades. It also provides automatic code
fixes to migrate call sites.

## Setup

### 1. Add the analyzer package

```xml
<ItemGroup>
  <PackageReference Include="WrapGod.Analyzers"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 2. Create a type mapping file

The analyzer reads wrapped type mappings from an **AdditionalFile** named
`*.wrapgod-types.txt`. Each line maps an original type to its generated
wrapper interface and facade class:

```
# Format: OriginalType -> WrapperInterface, FacadeType
Vendor.Lib.HttpClient -> IWrappedHttpClient, HttpClientFacade
Vendor.Lib.Logger -> IWrappedLogger, LoggerFacade
```

Lines starting with `#` are treated as comments and ignored. Empty lines
are skipped.

### 3. Include the mapping file

```xml
<ItemGroup>
  <AdditionalFiles Include="vendor-lib.wrapgod-types.txt" />
</ItemGroup>
```

## Diagnostics

### WG2001: Direct third-party type usage

| Property | Value |
|----------|-------|
| **ID** | `WG2001` |
| **Category** | `WrapGod.Usage` |
| **Default severity** | Warning |
| **Enabled by default** | Yes |
| **Message format** | `Type '{0}' has a generated wrapper interface; use '{1}' instead` |

**Trigger**: The analyzer reports WG2001 when it encounters an identifier
that resolves to a type listed in the mapping file. This covers:

- Local variable declarations (`HttpClient client = ...`)
- Parameter types
- Field and property types
- Any other identifier that resolves to the wrapped type's symbol

**Exclusion**: Identifiers that are part of a member access expression
(e.g. `client.SendAsync`) are handled separately by WG2002 and are not
double-reported.

### WG2002: Direct third-party method call

| Property | Value |
|----------|-------|
| **ID** | `WG2002` |
| **Category** | `WrapGod.Usage` |
| **Default severity** | Warning |
| **Enabled by default** | Yes |
| **Message format** | `Method '{0}' on type '{1}' should be called through the facade '{2}'` |

**Trigger**: The analyzer reports WG2002 when it encounters a method
invocation expression where the containing type of the invoked method is
listed in the mapping file. This fires on patterns like:

```csharp
client.SendAsync(request);   // WG2002
HttpClient.CreateDefault();  // WG2002 (static method)
```

Only method invocations are flagged -- property access and field access
are not reported by WG2002.

## Code fixes

Both diagnostics come with automatic code fix providers implemented in
`UseWrapperCodeFixProvider`.

### WG2001 fix: Use wrapper interface

The code fix replaces the type reference with the wrapper interface name
from the mapping file.

**Before**:

```csharp
HttpClient client = new HttpClient();
```

**After**:

```csharp
IWrappedHttpClient client = new HttpClient();
```

The fix preserves the original trivia (whitespace, comments) around the
replaced node.

### WG2002 fix: Use facade

The code fix replaces the receiver expression in the method call with the
facade type name.

**Before**:

```csharp
client.SendAsync(request);
```

**After**:

```csharp
HttpClientFacade.SendAsync(request);
```

### Fix All support

Both code fixes support the **Fix All** operation via
`WellKnownFixAllProviders.BatchFixer`. This allows you to apply all
WG2001 or WG2002 fixes across a document, project, or entire solution in
a single action.

**Equivalence keys** (used by the batch fixer to group fixes):

| Diagnostic | Equivalence key |
|------------|-----------------|
| WG2001 | `WG2001_UseWrapper` |
| WG2002 | `WG2002_UseFacade` |

### Applying fixes from the command line

Use `dotnet format` to apply analyzer fixes in CI or from the terminal:

```bash
# Fix all WrapGod diagnostics in the solution
dotnet format analyzers --diagnostics WG2001 WG2002

# Fix a specific project
dotnet format analyzers MyApp.csproj --diagnostics WG2001 WG2002
```

## Suppression

To suppress a diagnostic for a specific line:

```csharp
#pragma warning disable WG2001
HttpClient client = GetClient();  // intentional direct usage
#pragma warning restore WG2001
```

Or suppress at the project level in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.WG2001.severity = none
dotnet_diagnostic.WG2002.severity = none
```

## Architecture notes

- The analyzer uses `RegisterCompilationStartAction` to load mappings
  once per compilation, then `RegisterSyntaxNodeAction` for efficient
  per-node analysis.
- Concurrent execution is enabled for performance.
- Generated code is excluded from analysis
  (`GeneratedCodeAnalysisFlags.None`).
- Symbol resolution uses `SemanticModel.GetSymbolInfo` to accurately
  resolve types through aliases, usings, and implicit conversions.
