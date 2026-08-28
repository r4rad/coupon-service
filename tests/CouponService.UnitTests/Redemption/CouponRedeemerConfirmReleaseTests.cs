using System.Text.Json;
using CouponService.Application.Policies;
using CouponService.Application.Pricing;
using CouponService.Application.Redemption;
using CouponService.Application.Validation;
using CouponService.Domain;
using CouponService.Engine.Caching;
using CouponService.Engine.Facts;
using CouponService.Infrastructure.InMemory;
using PolicyEngine = CouponService.Application.Engine.PolicyEngine;

namespace CouponService.UnitTests.Redemption;

public sealed class CouponRedeemerConfirmReleaseTests
{
    [Fact]
    public async Task Confirm_commits_a_reserved_redemption()
    {
        var context = new RedemptionTestContext();
        await context.SeedPolicyAsync("SAVE10", RedemptionTestContext.Save10Document);
        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer();

        await context.Redeemer.ReserveAsync("SAVE10", "order-1", cart, customer);
        var result = await context.Redeemer.ConfirmAsync("order-1");
        var counter = await context.Redemptions.GetCounterAsync("SAVE10");

        Assert.True(result.Succeeded);
        Assert.Equal(RedemptionState.Confirmed, result.Redemption!.State);
        Assert.Null(result.Redemption.TtlExpiresAt);
        Assert.Equal(1, counter!.ConfirmedCount);
        Assert.Equal(0, counter.ActiveReservations);
    }

    [Fact]
    public async Task Release_returns_the_reserved_use_when_order_fails()
    {
        var context = new RedemptionTestContext();
        await context.SeedPolicyAsync("SAVE10", RedemptionTestContext.Save10Document);
        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer();

        await context.Redeemer.ReserveAsync("SAVE10", "order-1", cart, customer);
        var result = await context.Redeemer.ReleaseAsync("order-1", "payment-failed");
        var counter = await context.Redemptions.GetCounterAsync("SAVE10");

        Assert.True(result.Succeeded);
        Assert.Equal(RedemptionState.Released, result.Redemption!.State);
        Assert.Equal(0, counter!.ConfirmedCount);
        Assert.Equal(0, counter.ActiveReservations);
    }

    [Fact]
    public async Task Confirm_called_twice_is_a_no_op()
    {
        var context = new RedemptionTestContext();
        await context.SeedPolicyAsync("SAVE10", RedemptionTestContext.Save10Document);
        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer();

        await context.Redeemer.ReserveAsync("SAVE10", "order-1", cart, customer);
        var first = await context.Redeemer.ConfirmAsync("order-1");
        var writesAfterFirst = context.Redemptions.WriteCount;
        var second = await context.Redeemer.ConfirmAsync("order-1");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(RedemptionState.Confirmed, second.Redemption!.State);
        Assert.Equal(writesAfterFirst, context.Redemptions.WriteCount);
    }

    [Fact]
    public async Task Release_called_twice_is_a_no_op()
    {
        var context = new RedemptionTestContext();
        await context.SeedPolicyAsync("SAVE10", RedemptionTestContext.Save10Document);
        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer();

        await context.Redeemer.ReserveAsync("SAVE10", "order-1", cart, customer);
        var first = await context.Redeemer.ReleaseAsync("order-1", "payment-failed");
        var writesAfterFirst = context.Redemptions.WriteCount;
        var second = await context.Redeemer.ReleaseAsync("order-1", "payment-failed");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(RedemptionState.Released, second.Redemption!.State);
        Assert.Equal(writesAfterFirst, context.Redemptions.WriteCount);
    }
}

public sealed class CouponRedeemerExpireTests
{
    [Fact]
    public async Task Expired_reservation_stops_consuming_the_cap()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
        var policies = new InMemoryPolicyRepository();
        var redemptions = new InMemoryRedemptionRepository();
        var redeemer = CreateRedeemer(clock, policies, redemptions);
        await SeedLimitedPolicyAsync(policies);

        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer();

        var reserved = await redeemer.ReserveAsync("LIMITED1", "order-abandoned", cart, customer);
        Assert.True(reserved.Succeeded);

        clock.Advance(TimeSpan.FromMinutes(16));
        var afterExpiry = await redeemer.ReserveAsync("LIMITED1", "order-new", cart, customer);

        Assert.True(afterExpiry.Succeeded);
        var abandoned = await redemptions.FindByOrderIdAsync("order-abandoned");
        Assert.Equal(RedemptionState.Expired, abandoned!.State);
    }

    private static async Task SeedLimitedPolicyAsync(InMemoryPolicyRepository policies)
    {
        using var json = System.Text.Json.JsonDocument.Parse(RedemptionTestContext.LimitedOneUseDocument);
        var policyId = json.RootElement.GetProperty("policyId").GetString()!;

        await policies.CreateAsync(new PolicyRecord(
            "LIMITED1",
            policyId,
            "LIMITED1",
            PolicyTrigger.Code,
            RedemptionTestContext.LimitedOneUseDocument,
            string.Empty));
    }

    private static CouponRedeemer CreateRedeemer(
        IClock clock,
        InMemoryPolicyRepository policies,
        IRedemptionRepository redemptions)
    {
        var registry = StandardFactVocabulary.Create();
        var engine = new PolicyEngine(clock, registry, redemptions, new CompiledPolicyCache(clock));
        var validator = new CouponValidator(policies, engine);
        return new CouponRedeemer(validator, new PriceCalculator(), policies, redemptions, clock);
    }
}
