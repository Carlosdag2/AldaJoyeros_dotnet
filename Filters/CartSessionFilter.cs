using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AldaJoyeros.Filters;

// ASP.NET sessions do not lock concurrent requests. Serialize cart edits and account transfers.
public sealed class CartSessionFilter(Microsoft.Extensions.Options.IOptions<SessionOptions> options) : IAsyncResourceFilter
{
    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 128).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var session = context.HttpContext.Features.Get<ISessionFeature>()?.Session;
        if (session == null) { await next(); return; }
        // Reading Session.Id would load its data before acquiring the lock. Use the opaque cookie instead.
        var key = context.HttpContext.Request.Cookies[options.Value.Cookie.Name!] ?? context.HttpContext.TraceIdentifier;
        var gate = Gates[(int)((uint)StringComparer.Ordinal.GetHashCode(key) % Gates.Length)];
        await gate.WaitAsync(context.HttpContext.RequestAborted);
        try {
            await session.LoadAsync(context.HttpContext.RequestAborted);
            await next();
            await session.CommitAsync(context.HttpContext.RequestAborted);
        } finally { gate.Release(); }
    }
}
