using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

using ProductsApi.Products;

namespace ProductsApi.Caching;

/// <summary>The cache both instances share, kept in Redis.</summary>
internal sealed class DistributedProductCache(IDistributedCache cache) : IProductCache
{
    public async Task<Product?> GetAsync(int id)
    {
        var json = await cache.GetStringAsync($"product:{id}");
        return json is null ? null : JsonSerializer.Deserialize<Product>(json);
    }

    public Task SetAsync(Product product)
    {
        return cache.SetStringAsync(
            $"product:{product.Id}",
            JsonSerializer.Serialize(product),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
    }
}