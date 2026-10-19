using Microsoft.Extensions.Caching.Memory;

namespace AldaJoyeros.Services;

public sealed class PublicImageCache : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 32 * 1024 * 1024 });
    // Bound simultaneous decodes; original images are never rewritten in MongoDB.
    public SemaphoreSlim ConversionSlots { get; } = new(2);
    public bool TryGet(string key, out byte[]? value) => cache.TryGetValue(key, out value);
    public void Store(string key, byte[] value) => cache.Set(key, value, new MemoryCacheEntryOptions { Size = value.Length, SlidingExpiration = TimeSpan.FromMinutes(5) });
    public void Dispose() { cache.Dispose(); ConversionSlots.Dispose(); }
}
