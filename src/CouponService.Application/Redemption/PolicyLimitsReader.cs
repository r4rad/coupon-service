using System.Text.Json;

namespace CouponService.Application.Redemption;

internal static class PolicyLimitsReader
{
    internal static (int? TotalUses, int? PerCustomer) ReadLimits(string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        if (!document.RootElement.TryGetProperty("limits", out var limits)
            || limits.ValueKind is not JsonValueKind.Object)
        {
            return (null, null);
        }

        int? totalUses = null;
        int? perCustomer = null;

        if (limits.TryGetProperty("totalUses", out var totalUsesElement)
            && totalUsesElement.TryGetInt32(out var parsedTotalUses))
        {
            totalUses = parsedTotalUses;
        }

        if (limits.TryGetProperty("perCustomer", out var perCustomerElement)
            && perCustomerElement.TryGetInt32(out var parsedPerCustomer))
        {
            perCustomer = parsedPerCustomer;
        }

        return (totalUses, perCustomer);
    }
}
