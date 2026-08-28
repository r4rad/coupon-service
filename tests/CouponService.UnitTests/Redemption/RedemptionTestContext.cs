using System.Collections.Immutable;
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

internal sealed class RedemptionTestContext
{
    internal RedemptionTestContext()
    {
        Clock = new FixedClock(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
        Policies = new InMemoryPolicyRepository();
        Redemptions = new InMemoryRedemptionRepository();
        var registry = StandardFactVocabulary.Create();
        var engine = new PolicyEngine(Clock, registry, Redemptions, new CompiledPolicyCache(Clock));
        var validator = new CouponValidator(Policies, engine);
        Redeemer = new CouponRedeemer(
            validator,
            new PriceCalculator(),
            Policies,
            Redemptions,
            Clock);
    }

    internal IClock Clock { get; }

    internal InMemoryPolicyRepository Policies { get; }

    internal InMemoryRedemptionRepository Redemptions { get; }

    internal CouponRedeemer Redeemer { get; }

    internal static Cart CreateStandardCart() =>
        new(ImmutableArray.Create(
            new CartLine("line-1", "margherita", "classic", 9.50m, 2),
            new CartLine("line-2", "bbq-chicken", "meat", 12.00m, 1)));

    internal static CustomerContext CreateCustomer(string customerId = "customer-1") =>
        new(customerId, ConfirmedOrderCount: 0);

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

    internal static string LimitedOneUseDocument =>
        """
        {
          "policyId": "limited1",
          "code": "LIMITED1",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "limits": { "totalUses": 1 },
          "condition": {
            "lt": [ { "fact": "coupon.uses.total" }, 1 ]
          },
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

    internal static string PerCustomerOneUseDocument =>
        """
        {
          "policyId": "once-per-customer",
          "code": "ONCE1",
          "trigger": "code",
          "status": "Active",
          "engineSchema": "1.0",
          "limits": { "perCustomer": 1 },
          "condition": {
            "lt": [ { "fact": "coupon.uses.byCustomer" }, 1 ]
          },
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
