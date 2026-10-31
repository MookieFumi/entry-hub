namespace ProductsApi.Caching;

internal static class CachingExtensions
{
    // Cache:Mode is "memory" (default) or "redis". Cache:Redis holds the connection string.
    public static void AddProductCache(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration["Cache:Mode"] == "redis")
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = configuration["Cache:Redis"]);
            services.AddSingleton<IProductCache, DistributedProductCache>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<IProductCache, MemoryProductCache>();
        }
    }
}