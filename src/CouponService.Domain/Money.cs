namespace CouponService.Domain;

public static class Money
{
    public static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static decimal LineTotal(decimal unitPrice, int quantity) =>
        Round(unitPrice * quantity);

    public static decimal Percentage(decimal baseAmount, decimal percentage) =>
        Round(baseAmount * percentage / 100m);

    public static decimal CapDiscount(decimal discount, decimal baseAmount) =>
        Math.Min(Round(discount), Round(baseAmount));
}
