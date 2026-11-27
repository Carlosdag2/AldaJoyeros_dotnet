using Microsoft.EntityFrameworkCore;
namespace AldaJoyeros.Services;

// Cache only successful detections: an unavailable server must be retried later.
public sealed class MySqlServerVersionCache(Func<ServerVersion> detect)
{
    readonly object gate = new();
    ServerVersion? version;
    public ServerVersion Get()
    {
        lock (gate) return version ??= detect();
    }
}
