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

public sealed class CouponRedeemerReserveTests
{
    [Fact]
    public async Task Reserve_returns_authoritative_price_breakdown_when_checkout_begins()
    {
        var context = new RedemptionTestContext();
        await context.SeedPolicyAsync("SAVE10", RedemptionTestContext.Save10Document);
        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer();

        var result = await context.Redeemer.ReserveAsync("SAVE10", "order-1", cart, customer);

        Assert.True(result.Succeeded);
        Assert.Null(result.Reason);
        Assert.NotNull(result.Breakdown);
        Assert.Equal(31.00m, result.Breakdown!.Subtotal);
        Assert.Equal(3.10m, result.Breakdown.Discount);
        Assert.Equal(27.90m, result.Breakdown.Total);
        Assert.NotNull(result.Redemption);
        Assert.Equal(RedemptionState.Reserved, result.Redemption!.State);
    }

    [Fact]
    public async Task Reserve_stamps_ttl_while_reservation_is_reserved()
    {
        var context = new RedemptionTestContext();
        await context.SeedPolicyAsync("SAVE10", RedemptionTestContext.Save10Document);
        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer();
        var expectedExpiry = context.Clock.UtcNow.Add(TimeSpan.FromSeconds(900));

        var result = await context.Redeemer.ReserveAsync("SAVE10", "order-1", cart, customer);

        Assert.NotNull(result.Redemption?.TtlExpiresAt);
        Assert.Equal(expectedExpiry, result.Redemption!.TtlExpiresAt!.Value);
    }

    [Fact]
    public async Task Repeated_reserve_for_same_orderId_returns_existing_without_incrementing_counter()
    {
        var context = new RedemptionTestContext();
        await context.SeedPolicyAsync("LIMITED1", RedemptionTestContext.LimitedOneUseDocument);
        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer();

        var first = await context.Redeemer.ReserveAsync("LIMITED1", "order-1", cart, customer);
        var writesAfterFirst = context.Redemptions.WriteCount;
        var counterAfterFirst = await context.Redemptions.GetCounterAsync("LIMITED1");

        var second = await context.Redeemer.ReserveAsync("LIMITED1", "order-1", cart, customer);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Redemption!.OrderId, second.Redemption!.OrderId);
        Assert.Equal(writesAfterFirst, context.Redemptions.WriteCount);
        Assert.Equal(1, counterAfterFirst!.ActiveReservations);
        Assert.Equal(0, counterAfterFirst.ConfirmedCount);
    }

    [Fact]
    public async Task Concurrent_reservations_at_a_cap_with_one_use_left_grant_exactly_one()
    {
        var context = new RedemptionTestContext();
        await context.SeedPolicyAsync("LIMITED1", RedemptionTestContext.LimitedOneUseDocument);
        var cart = RedemptionTestContext.CreateStandardCart();

        var firstTask = context.Redeemer.ReserveAsync(
            "LIMITED1",
            "order-a",
            cart,
            RedemptionTestContext.CreateCustomer("customer-a"));
        var secondTask = context.Redeemer.ReserveAsync(
            "LIMITED1",
            "order-b",
            cart,
            RedemptionTestContext.CreateCustomer("customer-b"));

        var results = await Task.WhenAll(firstTask, secondTask);

        var successes = results.Count(result => result.Succeeded);
        var rejections = results.Where(result => !result.Succeeded).ToArray();

        Assert.Equal(1, successes);
        Assert.Single(rejections);
        Assert.Equal(RejectionReason.UsageLimitReached, rejections[0].Reason);
    }

    [Fact]
    public async Task Reserve_rejects_with_per_customer_limit_reached()
    {
        var context = new RedemptionTestContext();
        await context.SeedPolicyAsync("ONCE1", RedemptionTestContext.PerCustomerOneUseDocument);
        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer("loyal-customer");

        var first = await context.Redeemer.ReserveAsync("ONCE1", "order-1", cart, customer);
        await context.Redeemer.ConfirmAsync("order-1");

        var second = await context.Redeemer.ReserveAsync("ONCE1", "order-2", cart, customer);

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Equal(RejectionReason.PerCustomerLimitReached, second.Reason);
    }

    [Fact]
    public async Task ETag_conflict_retries_then_rejects_with_usage_limit_reached()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
        var repository = new FlakyCounterRedemptionRepository(failuresBeforeSuccess: 3);
        var policies = new InMemoryPolicyRepository();
        await SeedLimitedPolicyAsync(policies);

        var redeemer = CreateRedeemer(clock, policies, repository);
        var cart = RedemptionTestContext.CreateStandardCart();
        var customer = RedemptionTestContext.CreateCustomer();

        var result = await redeemer.ReserveAsync("LIMITED1", "order-1", cart, customer);

        Assert.False(result.Succeeded);
        Assert.Equal(RejectionReason.UsageLimitReached, result.Reason);
        Assert.Equal(3, repository.TryReserveAttempts);
    }

    private static async Task SeedLimitedPolicyAsync(InMemoryPolicyRepository policies)
    {
        using var json = JsonDocument.Parse(RedemptionTestContext.LimitedOneUseDocument);
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
