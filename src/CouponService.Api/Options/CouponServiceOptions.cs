using System.ComponentModel.DataAnnotations;

namespace CouponService.Api.Options;

public sealed class CouponServiceOptions
{
    public const string SectionName = "CouponService";

    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    public string Currency { get; init; } = "EUR";

    [Required]
    public string LocalTimeZoneId { get; init; } = "UTC";
}
