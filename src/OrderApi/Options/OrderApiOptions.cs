namespace OrderApi.Options;

public sealed class OrderApiOptions
{
    public const string SectionName = "OrderApi";

    public string CouponServiceBaseUrl { get; init; } = "https://localhost:7081";

    public string CouponServiceToken { get; init; } = string.Empty;

    public string PizzasFilePath { get; init; } = "data/pizzas.json";
}
