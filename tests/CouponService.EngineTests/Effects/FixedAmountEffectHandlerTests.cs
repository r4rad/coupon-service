namespace CouponService.EngineTests.Effects;

public sealed class FixedAmountEffectHandlerTests
{
    [Fact]
    public void Fixed_amount_exceeding_eligible_base_caps_discount_so_total_is_zero()
    {
        var cart = EffectsTestHelper.CreateSingleLineCart(3.00m);
        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
              "fixedAmount": { "amount": 5.00 }
            }
            """,
            cart);

        Assert.Equal(3.00m, plan.Total);
        Assert.Equal(3.00m, plan.Allocations.Single().Amount);

        var breakdown = new CouponService.Application.Pricing.PriceCalculator().Calculate(
            cart,
            new CouponService.Application.Pricing.PolicyDecision(
                CouponService.Application.Pricing.CouponStatus.Applied,
                plan));

        Assert.Equal(0m, breakdown.Total);
        Assert.Equal(3.00m, breakdown.Discount);
    }
}
