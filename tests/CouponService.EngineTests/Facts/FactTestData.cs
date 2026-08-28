using System.Collections.Immutable;
using CouponService.Domain;
using CouponService.Engine.Facts;

namespace CouponService.EngineTests.Facts;

internal static class FactTestData
{
    internal static EvalScope CreateScope(
        Cart? cart = null,
        CartLine? currentLine = null,
        int confirmedOrderCount = 0,
        bool isFirstOrder = false,
        int couponUsesTotal = 0,
        int couponUsesByCustomer = 0,
        DateTimeOffset? utcNow = null)
    {
        var clock = new FixedClock(utcNow ?? new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));

        return new EvalScope
        {
            Clock = clock,
            Cart = cart ?? CreateSampleCart(),
            CurrentLine = currentLine,
            ConfirmedOrderCount = confirmedOrderCount,
            IsFirstOrder = isFirstOrder,
            CouponUsesTotal = couponUsesTotal,
            CouponUsesByCustomer = couponUsesByCustomer,
        };
    }

    internal static Cart CreateSampleCart() =>
        new(
        [
            new CartLine("line-1", "margherita", "classic", 9.50m, 2),
            new CartLine("line-2", "bbq-chicken", "specialty", 12.00m, 1),
        ]);
}
