namespace OrderApi.Catalog;

public sealed record PizzaCatalogEntry(
    string Id,
    string Name,
    decimal UnitPrice,
    bool Vegetarian);

public sealed record PizzaCatalogSnapshot(
    string Currency,
    IReadOnlyList<PizzaCatalogEntry> Pizzas,
    string ETag);

public interface IPizzaCatalog
{
    PizzaCatalogSnapshot GetSnapshot();
}
