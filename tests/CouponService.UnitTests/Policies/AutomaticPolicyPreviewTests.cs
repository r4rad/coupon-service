using CouponService.Application.Policies;
using CouponService.Application.Pricing;
using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.UnitTests.Policies;

public sealed class AutomaticPolicyPreviewTests
{
    [Fact]
    public async Task Automatic_policy_applies_when_basket_is_previewed_without_a_code()
    {
        var context = new PoliciesTestContext(new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.Zero));
        await context.SeedAsync(PoliciesTestDocuments.TuesdayAutomatic);

        var decision = await context.AutomaticPreview.PreviewWithoutCodeAsync(
            PoliciesTestContext.CreateStandardCart(),
            new CustomerContext("customer-1"));

        Assert.Equal(CouponStatus.Applied, decision.Status);
        Assert.NotNull(decision.Plan);
        Assert.Equal(3.10m, decision.Plan!.Total);
    }
}
