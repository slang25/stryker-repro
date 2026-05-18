namespace StrykerRepro.Endpoints;

/// <summary>
/// Bug 1: CS9234 — RequestDelegateGenerator interceptor invalidation.
///
/// This is a CASCADE from Bug 4 (LinqMutator false positive).
///
/// RequestDelegateGenerator emits [InterceptsLocation(version: 1, data: "...")] in
/// GeneratedRouteBuilderExtensions.g.cs where 'data' is a base64-encoded struct
/// containing a SHA-256 content checksum of THIS file plus a byte offset.
///
/// Cascade chain:
///   1. Stryker instruments all files at once (including injecting a Prepend mutation
///      for IResponseCookies.Append). Source generator runs ONCE on the fully-
///      instrumented code and embeds checksums based on that instrumented content.
///   2. First compilation fails: CS1501 — LinqMutator generated
///      "Cookies.Prepend()" but IResponseCookies has no Prepend method.
///   3. CSharpRollbackProcess removes the Prepend mutation, modifying this file.
///   4. Stryker recompiles WITHOUT re-running source generators — it reuses the
///      GeneratedRouteBuilderExtensions.g.cs from step 1.
///   5. The checksum in [InterceptsLocation] no longer matches the rolled-back
///      file content → CS9234: "Cannot intercept a call in file
///      'GreetingEndpoints.cs' because a matching file was not found".
///   6. CS9234 is reported against the GENERATED file, not this one.
///      CSharpRollbackProcess cannot map it to any source mutation → fails
///      after 3 attempts and the entire Stryker run aborts.
///
/// Suggested Stryker fixes:
///   a) In CSharpRollbackProcess: parse the filename from the CS9234 message
///      ("cannot intercept call in file 'X'") and roll back all mutations in X.
///   b) Re-run source generators after each rollback step instead of reusing
///      the cached generator output from the first instrumented compilation.
/// </summary>
public static class GreetingEndpoints
{
    public static WebApplication MapGreetingEndpoints(this WebApplication app)
    {
        // MapGet/MapPost with typed parameters trigger RequestDelegateGenerator.
        // The source generator emits [InterceptsLocation(1, data)] pointing at the
        // exact byte position + checksum of this file content at the time of
        // instrumented compilation. After any rollback that changes this file,
        // the checksum is stale.
        app.MapGet("/greet", (string? name) =>
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Hello, World!";
            }

            return $"Hello, {name}!";
        });

        // Bug 4 (LinqMutator false positive) trigger: IResponseCookies.Append
        // is mutated to Prepend, causing CS1501. That rollback then changes this
        // file's bytes, making the source generator's checksums stale → CS9234.
        app.MapPost("/greet", (GreetingRequest req, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest("Name is required.");
            }

            // IResponseCookies.Append is NOT a LINQ method, but LinqMutator
            // still rewrites it to Prepend because it only pattern-matches
            // the method name "Append" without checking the receiver type.
            ctx.Response.Cookies.Append(
                "last-greeted",
                req.Name,
                new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict });

            return Results.Ok($"Hello, {req.Name}!");
        });

        return app;
    }
}

public record GreetingRequest(string Name);
