using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderApi.Options;

namespace OrderApi.Catalog;

public sealed class FilePizzaCatalog(IHostEnvironment environment, IOptions<OrderApiOptions> options)
    : IPizzaCatalog
{
    private readonly object _gate = new();
    private PizzaCatalogSnapshot? _cache;

    public PizzaCatalogSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _cache ??= LoadSnapshot();
        }
    }

    private PizzaCatalogSnapshot LoadSnapshot()
    {
        var path = ResolvePath(options.Value.PizzasFilePath);
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var currency = root.GetProperty("currency").GetString() ?? "EUR";
        var etag = ComputeEtag(json);
        var pizzas = root.GetProperty("pizzas")
            .EnumerateArray()
            .Select(element => new PizzaCatalogEntry(
                element.GetProperty("id").GetString() ?? string.Empty,
                element.GetProperty("name").GetString() ?? string.Empty,
                element.GetProperty("unitPrice").GetDecimal(),
                element.TryGetProperty("vegetarian", out var vegetarian) && vegetarian.GetBoolean()))
            .ToArray();

        return new PizzaCatalogSnapshot(currency, pizzas, etag);
    }

    private string ResolvePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        foreach (var root in new[] { environment.ContentRootPath, AppContext.BaseDirectory })
        {
            var candidate = Path.Combine(root, configuredPath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(environment.ContentRootPath, configuredPath);
    }

    private static string ComputeEtag(string json)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
    }
}
