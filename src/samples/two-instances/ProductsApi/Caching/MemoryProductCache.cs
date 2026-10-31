using Microsoft.Extensions.Caching.Memory;

using ProductsApi.Products;

namespace ProductsApi.Caching;

/// <summary>The cache each instance keeps in its own memory.</summary>
internal sealed class MemoryProductCache(IMemoryCache cache) : IProductCache
{
    public Task<Product?> GetAsync(int id)
    {
        return Task.FromResult(cache.TryGetValue(id, out Product? product) ? product : null);
    }

    public Task SetAsync(Product product)
    {
        cache.Set(product.Id, product, TimeSpan.FromMinutes(5));
        return Task.CompletedTask;
    }
}