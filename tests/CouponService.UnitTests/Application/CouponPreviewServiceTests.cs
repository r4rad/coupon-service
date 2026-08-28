using System.Collections.Immutable;
using System.Text.Json;
using CouponService.Application.Engine;
using CouponService.Application.Policies;
using CouponService.Application.Preview;
using CouponService.Application.Pricing;
using CouponService.Application.Redemption;
using CouponService.Application.Validation;
using CouponService.Domain;
using CouponService.Engine.Caching;
using CouponService.Engine.Facts;
using CouponService.Infrastructure.InMemory;

namespace CouponService.UnitTests.Application;

internal sealed class PreviewTestContext
{
    internal PreviewTestContext()
    {
        Clock = new FixedClock(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
        Policies = new InMemoryPolicyRepository();
        Redemptions = new InMemoryRedemptionRepository();
        Registry = StandardFactVocabulary.Create();
        Engine = new PolicyEngine(Clock, Registry, Redemptions, new CompiledPolicyCache(Clock));
        Validator = new CouponValidator(Policies, Engine);
        Preview = new CouponPreviewService(Validator, new PriceCalculator());
    }

    internal IClock Clock { get; }

    internal InMemoryPolicyRepository Policies { get; }

    internal InMemoryRedemptionRepository Redemptions { get; }

    internal IFactRegistry Registry { get; }

    internal PolicyEngine Engine { get; }

    internal CouponValidator Validator { get; }

    internal CouponPreviewService Preview { get; }

    internal static Cart CreateStandardCart() =>
        new(ImmutableArray.Create(
            new CartLine("line-1", "margherita", "classic", 9.50m, 2),
            new CartLine("line-2", "bbq-chicken", "meat", 12.00m, 1)));

    internal static string Save10Document =>
        """
        {
          "policyId": "save10",
          "code": "SAVE10",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 0 ] },
          "effect": {
            "percentage": {
              "value": 10,
              "of": {
                "lines": {
                  "where": { "gte": [ { "fact": "line.quantity" }, 1 ] }
                }
              }
            }
          }
        }
        """;

    internal static string MinimumOrderDocument =>
        """
        {
          "policyId": "min25",
          "code": "MIN25",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 25.00 ] },
          "effect": {
            "percentage": {
              "value": 10,
              "of": {
                "lines": {
                  "where": { "gte": [ { "fact": "line.quantity" }, 1 ] }
                }
              }
            }
          }
        }
        """;

    internal async Task SeedPolicyAsync(string code, string documentJson)
    {
        using var json = JsonDocument.Parse(documentJson);
        var policyId = json.RootElement.GetProperty("policyId").GetString()!;

        await Policies.CreateAsync(new PolicyRecord(
            code,
            policyId,
            code,
            PolicyTrigger.Code,
            documentJson,
            string.Empty));
    }
}

public sealed class CouponPreviewServiceTests
{
    [Fact]
    public async Task Preview_applied_coupon_returns_subtotal_discount_total_and_per_line_allocations()
    {
        var context = new PreviewTestContext();
        await context.SeedPolicyAsync("SAVE10", PreviewTestContext.Save10Document);

        var result = await context.Preview.PreviewAsync(
            "SAVE10",
            PreviewTestContext.CreateStandardCart(),
            new CustomerContext("customer-1"));

        Assert.Equal(CouponStatus.Applied, result.Decision.Status);
        Assert.Null(result.Decision.Reason);
        Assert.NotNull(result.Decision.Plan);
        Assert.Equal(31.00m, result.Breakdown.Subtotal);
        Assert.Equal(3.10m, result.Breakdown.Discount);
        Assert.Equal(27.90m, result.Breakdown.Total);
        Assert.Equal(3.10m, result.Decision.Plan!.Total);
        Assert.Equal(
            result.Decision.Plan.Total,
            result.Decision.Plan.Allocations.Sum(allocation => allocation.Amount));
    }

    [Fact]
    public async Task Preview_rejected_coupon_returns_reason_and_full_price_breakdown()
    {
        var context = new PreviewTestContext();
        await context.SeedPolicyAsync("MIN25", PreviewTestContext.MinimumOrderDocument);

        var cart = new Cart(ImmutableArray.Create(
            new CartLine("line-1", "margherita", "classic", 21.90m, 1)));

        var result = await context.Preview.PreviewAsync(
            "MIN25",
            cart,
            new CustomerContext("customer-1"));

        Assert.Equal(CouponStatus.Rejected, result.Decision.Status);
        Assert.Equal(RejectionReason.MinimumOrderNotMet, result.Decision.Reason);
        Assert.Null(result.Decision.Plan);
        Assert.Equal(21.90m, result.Breakdown.Subtotal);
        Assert.Equal(0m, result.Breakdown.Discount);
        Assert.Equal(21.90m, result.Breakdown.Total);
    }

    [Fact]
    public async Task Preview_performs_no_repository_writes()
    {
        var context = new PreviewTestContext();
        await context.SeedPolicyAsync("SAVE10", PreviewTestContext.Save10Document);
        var policyWritesBefore = context.Policies.WriteCount;
        var redemptionWritesBefore = context.Redemptions.WriteCount;

        _ = await context.Preview.PreviewAsync(
            "SAVE10",
            PreviewTestContext.CreateStandardCart(),
            new CustomerContext("customer-1"));

        Assert.Equal(policyWritesBefore, context.Policies.WriteCount);
        Assert.Equal(redemptionWritesBefore, context.Redemptions.WriteCount);
    }

    [Fact]
    public async Task Preview_records_policy_content_hash_on_the_decision()
    {
        var context = new PreviewTestContext();
        const string document = """
            {
              "policyId": "save10",
              "code": "SAVE10",
              "trigger": "code",
              "status": "Active",
              "engineSchema": "1.0",
              "condition": { "gte": [ { "fact": "cart.subtotal" }, 0 ] },
              "effect": {
                "percentage": {
                  "value": 10,
                  "of": {
                    "lines": {
                      "where": { "gte": [ { "fact": "line.quantity" }, 1 ] }
                    }
                  }
                }
              }
            }
            """;

        await context.SeedPolicyAsync("SAVE10", document);

        var result = await context.Preview.PreviewAsync(
            "SAVE10",
            PreviewTestContext.CreateStandardCart(),
            new CustomerContext("customer-1"));

        Assert.Equal(PolicyContentHasher.ComputeHash(document), result.Decision.PolicyContentHash);
    }
}
