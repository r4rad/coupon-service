using CouponService.Application.Policies;
using CouponService.Application.Pricing;
using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.UnitTests.Policies;

public sealed class PolicyCandidateResolverTests
{
    [Fact]
    public async Task Coded_and_automatic_candidates_resolve_by_priority_not_insertion_order()
    {
        var context = new PoliciesTestContext(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
        await context.SeedAsync(PoliciesTestDocuments.Save10Coded(priority: 50));
        await context.SeedAsync(PoliciesTestDocuments.SiteWideAutomatic(priority: 100, percentage: 5));

        var cart = PoliciesTestContext.CreateStandardCart();
        var customer = new CustomerContext("customer-1");

        var coded = await context.Policies.GetByCodeAsync("SAVE10");
        var automatic = (await context.Policies.ListAutomaticAsync()).Single();

        var codedDecision = await context.Engine.EvaluateAsync(coded!, cart, customer);
        var automaticDecision = await context.Engine.EvaluateAsync(automatic, cart, customer);

        var winner = context.Resolver.Resolve([
            new EvaluatedPolicyCandidate(automatic, automaticDecision),
            new EvaluatedPolicyCandidate(coded!, codedDecision),
        ]);

        Assert.Equal(CouponStatus.Applied, winner!.Status);
        Assert.Equal(1.55m, winner.Plan!.Total);
    }

    [Fact]
    public async Task Higher_priority_coded_policy_wins_over_automatic()
    {
        var context = new PoliciesTestContext(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
        await context.SeedAsync(PoliciesTestDocuments.SiteWideAutomatic(priority: 50, percentage: 20));
        await context.SeedAsync(PoliciesTestDocuments.Save10Coded(priority: 200));

        var cart = PoliciesTestContext.CreateStandardCart();
        var customer = new CustomerContext("customer-1");

        var coded = await context.Policies.GetByCodeAsync("SAVE10");
        var automatic = (await context.Policies.ListAutomaticAsync()).Single();

        var winner = context.Resolver.Resolve([
            new EvaluatedPolicyCandidate(coded!, await context.Engine.EvaluateAsync(coded!, cart, customer)),
            new EvaluatedPolicyCandidate(automatic, await context.Engine.EvaluateAsync(automatic, cart, customer)),
        ]);

        Assert.Equal(3.10m, winner!.Plan!.Total);
    }

    [Fact]
    public void Tie_breaks_in_the_customers_favour_by_discount_amount()
    {
        var resolver = new PolicyCandidateResolver();
        var lowDiscount = new EvaluatedPolicyCandidate(
            new PolicyRecord("AUTO#a", "a", null, PolicyTrigger.Automatic, "{}", string.Empty),
            PolicyDecision.Applied(new DiscountPlan(1.00m, []), "hash-a"));
        var highDiscount = new EvaluatedPolicyCandidate(
            new PolicyRecord("AUTO#b", "b", null, PolicyTrigger.Automatic, """{"priority":100}""", string.Empty),
            PolicyDecision.Applied(new DiscountPlan(2.00m, []), "hash-b"));

        var winner = resolver.Resolve([lowDiscount, highDiscount]);

        Assert.Equal(2.00m, winner!.Plan!.Total);
    }

    [Fact]
    public void Non_stackable_policy_blocks_lower_priority_candidates()
    {
        var resolver = new PolicyCandidateResolver();
        var blocker = new EvaluatedPolicyCandidate(
            new PolicyRecord(
                "AUTO#blocker",
                "blocker",
                null,
                PolicyTrigger.Automatic,
                """{"priority":100,"stackable":false}""",
                string.Empty),
            PolicyDecision.Applied(new DiscountPlan(1.00m, []), "hash-blocker"));
        var lowerPriority = new EvaluatedPolicyCandidate(
            new PolicyRecord(
                "AUTO#generous",
                "generous",
                null,
                PolicyTrigger.Automatic,
                """{"priority":50,"stackable":true}""",
                string.Empty),
            PolicyDecision.Applied(new DiscountPlan(5.00m, []), "hash-generous"));

        var winner = resolver.Resolve([lowerPriority, blocker]);

        Assert.Equal(1.00m, winner!.Plan!.Total);
    }
}
