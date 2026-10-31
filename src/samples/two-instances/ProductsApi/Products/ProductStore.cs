using System.Text.Json;

namespace ProductsApi.Products;

/// <summary>The "database" both instances share: a JSON file. Each product starts at a price of 10.</summary>
internal sealed class ProductStore(IConfiguration configuration)
{
    private readonly string _path = configuration["Store:Path"] ?? "products.json";

    private readonly Lock _lock = new();

    public Product Get(int id)
    {
        lock (_lock)
        {
            return Load().TryGetValue(id, out var product) ? product : new Product(id, $"Product {id}", 10m);
        }
    }

    public Product SetPrice(int id, decimal price)
    {
        lock (_lock)
        {
            var products = Load();
            var updated = new Product(id, $"Product {id}", price);
            products[id] = updated;
            File.WriteAllText(_path, JsonSerializer.Serialize(products));
            return updated;
        }
    }

    private Dictionary<int, Product> Load()
    {
        return File.Exists(_path)
            ? JsonSerializer.Deserialize<Dictionary<int, Product>>(File.ReadAllText(_path)) ?? []
            : [];
    }
}