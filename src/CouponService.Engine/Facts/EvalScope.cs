using CouponService.Domain;

namespace CouponService.Engine.Facts;

public sealed class EvalScope
{
    public required IClock Clock { get; init; }

    public required Cart Cart { get; init; }

    public CartLine? CurrentLine { get; init; }

    public int? ConfirmedOrderCount { get; init; }

    public bool? IsFirstOrder { get; init; }

    public int? CouponUsesTotal { get; init; }

    public int? CouponUsesByCustomer { get; init; }
}
