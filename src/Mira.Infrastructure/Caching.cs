using Microsoft.Extensions.Caching.Memory;

namespace Mira.Infrastructure;

internal static class Caching
{
    // Failures throw out of the factory and are not cached.
    public static async Task<T> Cached<T>(this IMemoryCache cache, string key, TimeSpan ttl, Func<Task<T>> create) =>
        (await cache.GetOrCreateAsync(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = ttl;
            return create();
        }))!;
}
