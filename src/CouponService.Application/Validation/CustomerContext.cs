namespace CouponService.Application.Validation;

public sealed record CustomerContext(
    string CustomerId,
    int ConfirmedOrderCount = 0);
