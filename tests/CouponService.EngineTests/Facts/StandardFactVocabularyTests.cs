using CouponService.Domain;
using CouponService.Engine.Ast;
using CouponService.Engine.Facts;

namespace CouponService.EngineTests.Facts;

public sealed class StandardFactVocabularyTests
{
    [Fact]
    public void Standard_vocabulary_registers_all_documented_fact_paths()
    {
        var registry = StandardFactVocabulary.Create();

        string[] expectedPaths =
        [
            "cart.lineCount",
            "cart.subtotal",
            "cart.totalQuantity",
            "coupon.uses.byCustomer",
            "coupon.uses.total",
            "customer.confirmedOrderCount",
            "customer.isFirstOrder",
            "line.category",
            "line.quantity",
            "line.unitPrice",
            "time.localDayOfWeek",
            "time.localHour",
            "time.utcNow",
        ];

        Assert.Equal(expectedPaths, registry.All.Select(fact => fact.Path));
    }

    [Fact]
    public async Task ResolveAsync_returns_cart_subtotal_from_scope()
    {
        var registry = StandardFactVocabulary.Create();
        var scope = FactTestData.CreateScope();

        var value = await registry.ResolveAsync("cart.subtotal", scope, CancellationToken.None);

        Assert.Equal(31.00m, value.GetNumber());
    }

    [Fact]
    public async Task ResolveAsync_returns_line_facts_within_quantifier_scope()
    {
        var registry = StandardFactVocabulary.Create();
        var line = new CartLine("line-1", "margherita", "Vegetarian", 9.50m, 2);
        var scope = FactTestData.CreateScope(currentLine: line);

        var category = await registry.ResolveAsync("line.category", scope, CancellationToken.None);
        var unitPrice = await registry.ResolveAsync("line.unitPrice", scope, CancellationToken.None);
        var quantity = await registry.ResolveAsync("line.quantity", scope, CancellationToken.None);

        Assert.Equal("Vegetarian", category.GetText());
        Assert.Equal(9.50m, unitPrice.GetNumber());
        Assert.Equal(2m, quantity.GetNumber());
    }

    [Fact]
    public async Task ResolveAsync_returns_time_facts_from_injected_clock()
    {
        var registry = StandardFactVocabulary.Create();
        var scope = FactTestData.CreateScope(utcNow: new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));

        var utcNow = await registry.ResolveAsync("time.utcNow", scope, CancellationToken.None);
        var dayOfWeek = await registry.ResolveAsync("time.localDayOfWeek", scope, CancellationToken.None);
        var hour = await registry.ResolveAsync("time.localHour", scope, CancellationToken.None);

        Assert.Equal(scope.Clock.UtcNow.ToUnixTimeSeconds(), utcNow.GetNumber());
        Assert.Equal(DayOfWeek.Friday.ToString(), dayOfWeek.GetText());
        Assert.Equal(15m, hour.GetNumber());
    }
}
