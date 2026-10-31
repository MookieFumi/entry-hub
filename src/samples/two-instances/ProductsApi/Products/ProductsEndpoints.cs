using ProductsApi.Caching;

namespace ProductsApi.Products;

internal static class ProductsEndpoints
{
    public static void MapProducts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products/{id:int}", async (int id, ProductStore store, IProductCache cache) =>
        {
            var product = await cache.GetAsync(id);
            if (product is null)
            {
                product = store.Get(id);
                await cache.SetAsync(product);
            }

            return product;
        }).RequireRateLimiting("per-instance");

        app.MapPut("/products/{id:int}/price/{price:decimal}", async (
            int id, decimal price, ProductStore store, IProductCache cache) =>
        {
            var product = store.SetPrice(id, price);
            await cache.SetAsync(product);
            return product;
        });
    }
}