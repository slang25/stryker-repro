# Stryker Repro — Compilation Failure Scenarios

Minimal ASP.NET Core (.NET 10) project that reproduces three Stryker compilation
bugs encountered when adopting Stryker 4.14.x against a project using:
- ASP.NET Core minimal APIs with `RequestDelegateGenerator` (net10.0 required for Bug 1)
- C# `required` members
- `is { Length: > 0 } varName` property-pattern variable declarations

All 13 tests pass with `dotnet test`. Run `dotnet stryker` from `StrykerRepro.Tests/`
to trigger all three failures.

---

## Bug 1 — CS9234: RequestDelegateGenerator Interceptor Invalidation (cascade from Bug 4)

> This bug only manifests as a side effect of Bug 4. See [Bug 4](#bug-4--linqmutator-false-positive-on-non-linq-append) below for the root cause.

**File:** `StrykerRepro/Endpoints/GreetingEndpoints.cs`  
**Requires:** .NET 10+ (for version-1 checksum-based `[InterceptsLocation]` format)

### How to reproduce

```
cd StrykerRepro.Tests
dotnet stryker
```

### Expected

Stryker completes mutation testing; any CS1501 from the `Prepend` mutation is
rolled back cleanly.

### Actual

```
[WRN] An unidentified mutation in GeneratedRouteBuilderExtensions.g.cs resulted
      in a compile error (at 61:1) with id: CS9234, message: Cannot intercept a
      call in file 'GreetingEndpoints.cs' because a matching file was not found
      in the compilation.
[FTL] Stryker.NET could not compile the project after mutation.
```

### Root cause (cascade from LinqMutator false positive)

.NET 10's `RequestDelegateGenerator` emits `[InterceptsLocation(version: 1, data: "…")]`
where `data` is a base64-encoded struct containing:
1. SHA-256 checksum of the source file content **as it appears in the instrumented compilation**
2. Byte offset of the intercepted call

Cascade chain:

1. Stryker instruments all files at once (including `IResponseCookies.Append` → `Prepend`
   via `LinqMutator`). Source generators run **once** on this fully-instrumented code;
   checksums are computed from the instrumented file content.
2. First compilation fails: **CS1501** — `IResponseCookies.Prepend(string, string, CookieOptions)`
   doesn't exist.
3. `CSharpRollbackProcess` removes the `Prepend` mutation, modifying `GreetingEndpoints.cs`.
4. Stryker recompiles **without re-running source generators** — it reuses the
   `GeneratedRouteBuilderExtensions.g.cs` from step 1.
5. The checksum in `[InterceptsLocation]` no longer matches the rolled-back content →
   **CS9234**: "cannot intercept … because a matching file was not found".
6. CS9234 is reported against `GeneratedRouteBuilderExtensions.g.cs`, not `GreetingEndpoints.cs`.
   `CSharpRollbackProcess` cannot trace it to any source mutation → fails after 3 attempts.

**Why net8.0 doesn't trigger this:** net8.0 uses `[InterceptsLocation(filePath, line, column)]`.
When source generators re-run on the instrumented code, they emit updated line/column
interceptors. After rollback, if the line still exists, the interceptor remains valid.
The checksum-based version-1 format (net10.0+) is content-hash-sensitive: any byte change
invalidates the checksum.

### Suggested fixes in Stryker

**Fix A (rollback):** In `CSharpRollbackProcess`, when a CS9234 error is encountered,
parse the quoted filename from the diagnostic message ("a matching file was not found
in … 'GreetingEndpoints.cs'") and roll back all mutations in that source file.

**Fix B (root cause):** In `LinqMutator`, before substituting `Append` → `Prepend`,
verify the receiver type implements `IEnumerable<T>` or is `System.Linq.Enumerable`.
`IResponseCookies.Append` has a completely different signature and is not a LINQ method.
This also fixes Bug 4 directly.

**Fix C (robustness):** Re-run source generators after each rollback step rather than
reusing the first instrumented compilation's generator output. This would make Stryker
robust against any source generator that embeds checksums or positions.

---

## Bug 2 — Variable Shadowing with `is { Length: > 0 } varName` Patterns

**File:** `StrykerRepro/Options/AppOptionsExtensions.cs`

### How to reproduce

Point Stryker at `StrykerRepro.csproj`. The variable shadowing errors appear in the
first compilation attempt, after which `CSharpRollbackProcess` identifies and removes
the `RelationalPatternMutator` mutations.

### Expected

Stryker instruments the conditions and produces a valid compilation.

### Actual

```
CS0136: A local variable or function named 'region' is already defined in this scope
CS0136: A local variable or function named 'environmentName' is already defined in this scope
CS0165: Use of unassigned local variable 'region'
```

(Rolled back after 3 attempts; affected mutants get `CompileError` status.)

### Root cause

`RelationalPatternMutator` generates mutation variants for the `> 0` operator
in `is { Length: > 0 } region`, substituting `< 0` and `>= 0`. When these
variants are embedded inside the `PostConfigure` lambda body by
`ConditionalInstrumentationEngine`, each copy of the `is` pattern re-declares
the pattern variable in the same lambda scope → CS0136.

```csharp
// Stryker-instrumented version (simplified):
if (activeId == 29 ? cfg is { Length: < 0 } region
  : activeId == 30 ? cfg is { Length: >= 0 } region
  :                  cfg is { Length: > 0 } region)  // ← 'region' declared 3×
```

### Suggested fix in Stryker

`ConditionalInstrumentationEngine` should detect declaration patterns
(`is T name`, `is { … } name`) in `if` conditions. Before generating mutation
variants, the engine should either:
- Lift the match result into a separate `out`/`var` binding before the ternary, or
- Use a `switch` expression with explicit variable introduction that avoids scope conflicts.

---

## Bug 3 — ObjectCreationMutator + `required` Members

**File:** `StrykerRepro/Services/NotificationService.cs`

### How to reproduce

Point Stryker at `StrykerRepro.csproj`. The compilation error occurs in the
first attempt, alongside Bug 2.

### Expected

`ObjectCreationMutator` generates valid mutations for `new Notification { … }`.

### Actual

```
CS8852: Required member 'Notification.Id' must be set in the object initializer.
CS8852: Required member 'Notification.Recipient' must be set in the object initializer.
CS8852: Required member 'Notification.Body' must be set in the object initializer.
```

(Rolled back after 3 attempts; mutant 48 gets `CompileError` status.)

### Root cause

`ObjectCreationMutator` always generates an empty-initialiser mutation
(`new Notification {}`). C# 11+ `required` properties must be set at the
construction site; the empty initialiser violates this.

### Suggested fix in Stryker

In `ObjectCreationMutator`, after resolving the type symbol, skip the
empty-initialiser mutation if any member satisfies `IPropertySymbol.IsRequired`
or `IFieldSymbol.IsRequired`. The semantic model is already available during
mutation analysis.

---

## Bug 4 — LinqMutator False Positive on Non-LINQ `Append`

**File:** `StrykerRepro/Endpoints/GreetingEndpoints.cs`

### How to reproduce

Point Stryker at `StrykerRepro.csproj`. The compilation error occurs in the
first instrumented compilation. On .NET 10+ this rollback then cascades into
[Bug 1](#bug-1--cs9234-requestdelegategenerator-interceptor-invalidation-cascade-from-bug-4).

### Expected

`LinqMutator` only rewrites methods on LINQ-shaped receivers (e.g.
`IEnumerable<T>` or `System.Linq.Enumerable`). Calls to unrelated methods that
happen to be named `Append` are left untouched.

### Actual

```
CS1501: No overload for method 'Prepend' takes 3 arguments
```

`LinqMutator` rewrites `ctx.Response.Cookies.Append(name, value, options)` to
`ctx.Response.Cookies.Prepend(name, value, options)`. `IResponseCookies` has no
`Prepend` method, so the instrumented compilation fails.

### Root cause

`LinqMutator` pattern-matches on the *method name* `Append` without inspecting
the receiver type. `Microsoft.AspNetCore.Http.IResponseCookies.Append` has
nothing to do with LINQ, but the mutator still substitutes `Prepend` because
the method name matches a known LINQ operator.

### Suggested fix in Stryker

In `LinqMutator`, before substituting `Append` → `Prepend` (and similarly for
other LINQ method name swaps), use the semantic model to verify the receiver
type implements `IEnumerable<T>` or that the method is declared on
`System.Linq.Enumerable` / `System.Linq.Queryable`. This also resolves Bug 1
directly by preventing the rollback that invalidates the source generator's
interceptor checksums.

---

## Project structure

```
StrykerRepro.slnx                     ← XML solution file (new .slnx format)
global.json                           ← pins SDK to 10.0.300 (Bug 1 requires .NET 10)
├── StrykerRepro/                     ← source project to mutate (net10.0)
│   ├── Endpoints/
│   │   └── GreetingEndpoints.cs      ← Bug 1 (CS9234 cascade) + Bug 4 (LinqMutator)
│   ├── Options/
│   │   ├── AppOptions.cs
│   │   └── AppOptionsExtensions.cs   ← Bug 2 (variable shadowing)
│   ├── Services/
│   │   └── NotificationService.cs    ← Bug 3 (required members)
│   ├── Messages/
│   │   └── Notification.cs           ← Bug 3 type definition
│   └── Program.cs
└── StrykerRepro.Tests/               ← run `dotnet stryker` from here
    ├── stryker-config.json
    ├── GreetingEndpointsTests.cs
    ├── AppOptionsConfiguratorTests.cs
    └── NotificationServiceTests.cs
```
