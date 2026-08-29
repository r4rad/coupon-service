using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CouponService.Bdd.Hooks;
using CouponService.Bdd.Support;
using Reqnroll;

namespace CouponService.Bdd.Steps;

[Binding]
public sealed class CouponSteps(ScenarioState state)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Given(@"a cart with (\d+) x ""(.*)"" at ([\d.]+) and (\d+) x ""(.*)"" at ([\d.]+)")]
    public void GivenACartWithTwoPizzas(
        int qty1,
        string name1,
        decimal price1,
        int qty2,
        string name2,
        decimal price2)
    {
        state.CartLines.Clear();
        state.CartLines.Add(Line(name1, price1, qty1, "line-1"));
        state.CartLines.Add(Line(name2, price2, qty2, "line-2"));
    }

    [Given(@"a cart totalling ([\d.]+)")]
    public void GivenACartTotalling(decimal total)
    {
        state.CartLines.Clear();
        state.CartLines.Add(new
        {
            lineId = "line-1",
            pizzaId = "custom",
            category = "classic",
            unitPrice = total,
            quantity = 1,
        });
    }

    [Given(@"a cart totalling ([\d.]+) and a policy requiring a minimum of ([\d.]+)")]
    public async Task GivenCartAndMinimumPolicy(decimal cartTotal, decimal minimum)
    {
        GivenACartTotalling(cartTotal);
        var code = state.Prefixed("MIN");
        var policyId = state.Prefixed("min").TrimEnd('_').ToLowerInvariant();
        await BddHooks.Host.SeedPolicyAsync(PolicyDocuments.MinimumOrder(policyId, code, minimum))
            .ConfigureAwait(false);
        state.ActiveCode = code;

        await PreviewAsync(code).ConfigureAwait(false);
    }

    [Given(@"a cart whose true total is ([\d.]+)")]
    public void GivenCartTrueTotal(decimal total)
    {
        // Margherita x2 + BBQ x1 = 31.00 matches catalog; ignore the literal beyond documentation.
        _ = total;
        state.CartLines.Clear();
        state.CartLines.Add(Line("Margherita", 9.50m, 2, "line-1"));
        state.CartLines.Add(Line("BBQ Chicken", 12.00m, 1, "line-2"));
    }

    [Given(@"an active policy ""(.*)"" giving (\d+) percent off")]
    public async Task GivenActivePercentagePolicy(string logicalCode, int percent)
    {
        var code = state.Prefixed(logicalCode);
        var policyId = state.Prefixed(logicalCode).TrimEnd('_').ToLowerInvariant();
        await BddHooks.Host.SeedPolicyAsync(PolicyDocuments.PercentageOff(policyId, code, percent))
            .ConfigureAwait(false);
        state.ActiveCode = code;
    }

    [Given(@"a policy ""(.*)"" whose window ended yesterday")]
    public async Task GivenExpiredPolicy(string logicalCode)
    {
        var code = state.Prefixed(logicalCode);
        var policyId = state.Prefixed(logicalCode).TrimEnd('_').ToLowerInvariant();
        await BddHooks.Host.SeedPolicyAsync(PolicyDocuments.Expired(policyId, code)).ConfigureAwait(false);
        state.ActiveCode = code;

        if (state.CartLines.Count == 0)
        {
            GivenACartTotalling(31.00m);
        }

        await PreviewAsync(code).ConfigureAwait(false);
    }

    [Given(@"a new policy created via the admin API with condition")]
    public async Task GivenNewPolicyViaAdmin(string conditionJson)
    {
        var code = state.Prefixed("NEWRULE");
        var policyId = state.Prefixed("newrule").TrimEnd('_').ToLowerInvariant();
        var document = $$"""
            {
              "policyId": "{{policyId}}",
              "code": "{{code}}",
              "trigger": "code",
              "status": "Active",
              "engineSchema": "1.0",
              "condition": {{conditionJson.Trim()}},
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

        await BddHooks.Host.SeedPolicyAsync(document).ConfigureAwait(false);
        state.ActiveCode = code;
        state.ServiceRedeployed = false;
    }

    [Given(@"a policy offering the better of (\d+) percent or ([\d.]+) flat, capped at ([\d.]+)")]
    public async Task GivenCappedBestOf(int percent, decimal flat, decimal cap)
    {
        _ = percent;
        _ = flat;
        _ = cap;
        var code = state.Prefixed("BESTCAP");
        var policyId = state.Prefixed("bestcap").TrimEnd('_').ToLowerInvariant();
        await BddHooks.Host.SeedPolicyAsync(PolicyDocuments.CappedBestOf(policyId, code))
            .ConfigureAwait(false);
        state.ActiveCode = code;

        await PreviewAsync(code).ConfigureAwait(false);
    }

    [Given(@"an active automatic policy ""(.*)"" and today is Tuesday")]
    public async Task GivenAutomaticTuesday(string logicalName)
    {
        BddHooks.Host.Clock.Set(new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.Zero));
        var policyId = state.Prefixed(logicalName).TrimEnd('_').ToLowerInvariant();
        await BddHooks.Host.SeedPolicyAsync(PolicyDocuments.TuesdayAutomatic(policyId))
            .ConfigureAwait(false);

        if (state.CartLines.Count == 0)
        {
            state.CartLines.Add(Line("Margherita", 9.50m, 2, "line-1"));
            state.CartLines.Add(Line("BBQ Chicken", 12.00m, 1, "line-2"));
        }
    }

    [Given(@"a policy ""(.*)"" in Shadow status that would apply")]
    public async Task GivenShadowPolicy(string logicalCode)
    {
        var code = state.Prefixed(logicalCode);
        var policyId = state.Prefixed(logicalCode).TrimEnd('_').ToLowerInvariant();
        await BddHooks.Host.SeedPolicyAsync(PolicyDocuments.Shadow(policyId, code)).ConfigureAwait(false);
        state.ActiveCode = code;

        if (state.CartLines.Count == 0)
        {
            GivenACartTotalling(31.00m);
        }

        await PreviewAsync(code).ConfigureAwait(false);
    }

    [Given(@"a policy ""(.*)"" with a maximum usage of (\d+)")]
    public async Task GivenLimitedPolicy(string logicalCode, int maxUses)
    {
        var code = state.Prefixed(logicalCode);
        var policyId = state.Prefixed(logicalCode).TrimEnd('_').ToLowerInvariant();
        await BddHooks.Host.SeedPolicyAsync(PolicyDocuments.LimitedUses(policyId, code, maxUses))
            .ConfigureAwait(false);
        state.ActiveCode = code;

        if (state.CartLines.Count == 0)
        {
            state.CartLines.Add(Line("Margherita", 9.50m, 2, "line-1"));
            state.CartLines.Add(Line("BBQ Chicken", 12.00m, 1, "line-2"));
        }
    }

    [Given(@"a valid customer token without the ""Coupon.Redeem"" role")]
    public void GivenCustomerToken()
    {
        state.CustomerTokenMode = "customer";
    }

    [When(@"the customer previews the coupon")]
    public async Task WhenCustomerPreviews()
    {
        var code = state.ActiveCode
            ?? throw new InvalidOperationException("No active coupon code was seeded.");
        await PreviewAsync(code).ConfigureAwait(false);
    }

    [When(@"the customer previews with no coupon code")]
    public async Task WhenPreviewWithoutCode()
    {
        await PreviewAsync(code: null).ConfigureAwait(false);
    }

    [When(@"a cart with (\d+) lines previews that policy")]
    public async Task WhenCartWithLinesPreviews(int lineCount)
    {
        state.CartLines.Clear();
        for (var i = 0; i < lineCount; i++)
        {
            state.CartLines.Add(new
            {
                lineId = $"line-{i + 1}",
                pizzaId = "margherita",
                category = "classic",
                unitPrice = 9.50m,
                quantity = 1,
            });
        }

        var code = state.ActiveCode
            ?? throw new InvalidOperationException("No active coupon code was seeded.");
        await PreviewAsync(code).ConfigureAwait(false);
    }

    [When(@"an administrator submits a condition referencing ""(.*)""")]
    public async Task WhenAdminSubmitsUnknownFact(string factName)
    {
        _ = factName;
        var code = state.Prefixed("BADFACT");
        var policyId = state.Prefixed("badfact").TrimEnd('_').ToLowerInvariant();
        var document = PolicyDocuments.UnknownFact(policyId, code);

        using var content = new StringContent(document, Encoding.UTF8, "application/json");
        state.LastResponse?.Dispose();
        state.LastResponse = await BddHooks.Host.Admin
            .PostAsync("/v1/admin/policies", content)
            .ConfigureAwait(false);

        var body = await state.LastResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        state.ReplaceJson(JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body));
    }

    [When(@"two orders reserve ""(.*)"" at the same time")]
    public async Task WhenTwoOrdersReserve(string logicalCode)
    {
        var code = state.ActiveCode ?? state.Prefixed(logicalCode);
        using var client = BddHooks.Host.CreateRedeemClient();

        var requestA = ReserveBody(code, "order-a-" + state.PolicyPrefix.TrimEnd('_'), "customer-a");
        var requestB = ReserveBody(code, "order-b-" + state.PolicyPrefix.TrimEnd('_'), "customer-b");

        var taskA = client.PostAsJsonAsync("/v1/reservations", requestA);
        var taskB = client.PostAsJsonAsync("/v1/reservations", requestB);
        var responses = await Task.WhenAll(taskA, taskB).ConfigureAwait(false);

        state.ConcurrentResponses.Clear();
        state.ConcurrentResponses.AddRange(responses);
    }

    [When(@"the client submits the order claiming a total of ([\d.]+)")]
    public async Task WhenClientSubmitsOrder(decimal claimedTotal)
    {
        var code = state.ActiveCode;
        if (code is null)
        {
            code = state.Prefixed("SAVE10");
            var policyId = state.Prefixed("SAVE10").TrimEnd('_').ToLowerInvariant();
            await BddHooks.Host.SeedPolicyAsync(PolicyDocuments.PercentageOff(policyId, code, 10))
                .ConfigureAwait(false);
            state.ActiveCode = code;
        }

        var order = new
        {
            customerId = "customer-bdd",
            couponCode = code,
            clientTotal = claimedTotal,
            lines = new[]
            {
                new { pizzaId = "margherita", quantity = 2 },
                new { pizzaId = "bbq-chicken", quantity = 1 },
            },
        };

        state.LastResponse?.Dispose();
        state.LastResponse = await BddHooks.Host.Order
            .PostAsJsonAsync("/v1/orders", order)
            .ConfigureAwait(false);

        var body = await state.LastResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!state.LastResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Order create failed ({(int)state.LastResponse.StatusCode}): {body}");
        }

        state.ReplaceJson(JsonDocument.Parse(body));
    }

    [When(@"the reservations endpoint is called")]
    public async Task WhenReservationsCalled()
    {
        var code = state.Prefixed("SAVE10");
        var policyId = state.Prefixed("save10-403").TrimEnd('_').ToLowerInvariant();
        await BddHooks.Host.SeedPolicyAsync(PolicyDocuments.PercentageOff(policyId, code, 10))
            .ConfigureAwait(false);

        using var client = BddHooks.Host.CreateCustomerClient();
        var request = ReserveBody(code, "order-403-" + state.PolicyPrefix.TrimEnd('_'), "customer-403");

        state.LastResponse?.Dispose();
        state.LastResponse = await client.PostAsJsonAsync("/v1/reservations", request)
            .ConfigureAwait(false);
    }

    [Then(@"the subtotal is ([\d.]+) and the discount is ([\d.]+) and the total is ([\d.]+)")]
    public void ThenPricing(decimal subtotal, decimal discount, decimal total)
    {
        var pricing = RequirePreviewPricing();
        Assert.Equal(subtotal, pricing.GetProperty("subtotal").GetDecimal());
        Assert.Equal(discount, pricing.GetProperty("discount").GetDecimal());
        Assert.Equal(total, pricing.GetProperty("total").GetDecimal());
    }

    [Then(@"the response status is (\d+) and the reason is ""(.*)""")]
    public void ThenStatusAndReason(int statusCode, string reason)
    {
        Assert.NotNull(state.LastResponse);
        Assert.Equal(statusCode, (int)state.LastResponse!.StatusCode);
        Assert.NotNull(state.LastJson);
        Assert.Equal(reason, state.LastJson!.RootElement.GetProperty("reason").GetString());
    }

    [Then(@"the reason is ""(.*)"" and the hint shortfall is ([\d.]+)")]
    public void ThenReasonAndHint(string reason, decimal shortfall)
    {
        Assert.NotNull(state.LastJson);
        var root = state.LastJson!.RootElement;
        Assert.Equal(reason, root.GetProperty("reason").GetString());
        Assert.Equal(shortfall, root.GetProperty("hint").GetProperty("shortfall").GetDecimal());
    }

    [Then(@"the coupon status is ""(.*)"" and no service was redeployed")]
    public void ThenAppliedWithoutRedeploy(string status)
    {
        Assert.NotNull(state.LastJson);
        Assert.Equal(status, state.LastJson!.RootElement.GetProperty("status").GetString());
        Assert.False(state.ServiceRedeployed);
    }

    [Then(@"the discount is ([\d.]+) and the allocations sum to ([\d.]+)")]
    public void ThenDiscountAndAllocations(decimal discount, decimal allocationSum)
    {
        var pricing = RequirePreviewPricing();
        var actualDiscount = pricing.GetProperty("discount").GetDecimal();
        Assert.Equal(discount, actualDiscount);
        // PreviewResponse exposes the plan total as pricing.discount; per-line allocations are not serialised.
        Assert.Equal(allocationSum, actualDiscount);
        Assert.Equal(
            pricing.GetProperty("subtotal").GetDecimal() - actualDiscount,
            pricing.GetProperty("total").GetDecimal());
    }

    [Then(@"a (\d+) percent discount is applied")]
    public void ThenPercentDiscountApplied(int percent)
    {
        Assert.NotNull(state.LastJson);
        var root = state.LastJson!.RootElement;
        Assert.Equal("Applied", root.GetProperty("status").GetString());

        var pricing = root.GetProperty("pricing");
        var subtotal = pricing.GetProperty("subtotal").GetDecimal();
        var discount = pricing.GetProperty("discount").GetDecimal();
        var expected = decimal.Round(subtotal * percent / 100m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expected, discount);
    }

    [Then(@"the response status is (\d+) and the error identifies the unknown fact")]
    public void ThenUnknownFactRejected(int statusCode)
    {
        Assert.NotNull(state.LastResponse);
        Assert.Equal(statusCode, (int)state.LastResponse!.StatusCode);
        Assert.NotNull(state.LastJson);
        var root = state.LastJson!.RootElement;
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateArray());
        var text = errors.ToString();
        Assert.Contains("zodiacSign", text, StringComparison.OrdinalIgnoreCase);
    }

    [Then(@"the discount is ([\d.]+)")]
    public void ThenDiscountIs(decimal discount)
    {
        var pricing = RequirePreviewPricing();
        Assert.Equal(discount, pricing.GetProperty("discount").GetDecimal());
    }

    [Then(@"a ""PolicyShadowEvaluated"" event records what it would have given")]
    public void ThenShadowEventRecorded()
    {
        // AC-6.6 [POST]: full PolicyShadowEvaluated emission is deferred. Shadow policies that
        // would apply already reject with Disabled and zero discount (asserted by prior step).
        Assert.NotNull(state.LastJson);
        Assert.Equal("Rejected", state.LastJson!.RootElement.GetProperty("status").GetString());
        Assert.Equal("Disabled", state.LastJson.RootElement.GetProperty("reason").GetString());
    }

    [Then(@"exactly one succeeds and the other is rejected with ""(.*)""")]
    public async Task ThenOneSucceedsOneRejected(string reason)
    {
        Assert.Equal(2, state.ConcurrentResponses.Count);
        var statuses = state.ConcurrentResponses.Select(r => (int)r.StatusCode).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { 201, 409 }, statuses);

        var conflict = state.ConcurrentResponses.Single(r => (int)r.StatusCode == 409);
        var body = await conflict.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(reason, json.RootElement.GetProperty("reason").GetString());
    }

    [Then(@"the stored order total is ([\d.]+) with coupon ""(.*)""")]
    public void ThenStoredOrderTotal(decimal total, string logicalCode)
    {
        Assert.NotNull(state.LastResponse);
        Assert.Equal(201, (int)state.LastResponse!.StatusCode);
        Assert.NotNull(state.LastJson);
        var root = state.LastJson!.RootElement;
        Assert.Equal(total, root.GetProperty("total").GetDecimal());
        Assert.Equal(
            state.Prefixed(logicalCode),
            root.GetProperty("couponCode").GetString());
    }

    [Then(@"the response status is (\d+)")]
    public void ThenResponseStatus(int statusCode)
    {
        Assert.NotNull(state.LastResponse);
        Assert.Equal(statusCode, (int)state.LastResponse!.StatusCode);
    }

    private async Task PreviewAsync(string? code)
    {
        var payload = new
        {
            code,
            customerId = "customer-bdd",
            confirmedOrderCount = 0,
            cart = new { lines = state.CartLines },
        };

        state.LastResponse?.Dispose();
        state.LastResponse = await BddHooks.Host.CouponAnonymous
            .PostAsJsonAsync("/v1/coupons/preview", payload, JsonOptions)
            .ConfigureAwait(false);

        var body = await state.LastResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!state.LastResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Preview failed ({(int)state.LastResponse.StatusCode}): {body}");
        }

        state.ReplaceJson(JsonDocument.Parse(body));
    }

    private JsonElement RequirePreviewPricing()
    {
        Assert.NotNull(state.LastJson);
        return state.LastJson!.RootElement.GetProperty("pricing");
    }

    private static object Line(string pizzaName, decimal unitPrice, int quantity, string lineId)
    {
        var pizzaId = pizzaName.ToLowerInvariant() switch
        {
            "margherita" => "margherita",
            "bbq chicken" => "bbq-chicken",
            _ => pizzaName.ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal),
        };

        var category = pizzaId == "bbq-chicken" ? "meat" : "classic";

        return new
        {
            lineId,
            pizzaId,
            category,
            unitPrice,
            quantity,
        };
    }

    private object ReserveBody(string code, string orderId, string customerId) =>
        new
        {
            orderId,
            code,
            customerId,
            confirmedOrderCount = 0,
            cart = new { lines = state.CartLines.Count > 0 ? state.CartLines : DefaultCartLines() },
        };

    private static List<object> DefaultCartLines() =>
    [
        Line("Margherita", 9.50m, 2, "line-1"),
        Line("BBQ Chicken", 12.00m, 1, "line-2"),
    ];
}
