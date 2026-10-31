using ProductsApi.Products;

namespace ProductsApi.Caching;

internal interface IProductCache
{
    Task<Product?> GetAsync(int id);

    Task SetAsync(Product product);
}