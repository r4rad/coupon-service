using CouponService.Engine.Ast;
using CouponService.Engine.Facts;

namespace CouponService.EngineTests.Facts;

public sealed class FactRegistryTests
{
    [Fact]
    public void Register_rejects_duplicate_fact_paths()
    {
        var builder = new FactRegistryBuilder();
        var descriptor = new FactDescriptor(
            "cart.subtotal",
            ValueKind.Number,
            FactCost.Pure,
            static (_, _) => ValueTask.FromResult(Value.Of(0m)));

        builder.Register(descriptor);

        Assert.Throws<DuplicateFactRegistrationException>(() => builder.Register(descriptor));
    }

    [Fact]
    public async Task ResolveAsync_throws_when_fact_path_is_not_registered()
    {
        var registry = new FactRegistryBuilder()
            .Register(new FactDescriptor(
                "cart.subtotal",
                ValueKind.Number,
                FactCost.Pure,
                static (_, _) => ValueTask.FromResult(Value.Of(0m))))
            .Build();

        var scope = FactTestData.CreateScope();

        await Assert.ThrowsAsync<UnknownFactException>(() =>
            registry.ResolveAsync("customer.zodiacSign", scope, CancellationToken.None).AsTask());
    }
}
