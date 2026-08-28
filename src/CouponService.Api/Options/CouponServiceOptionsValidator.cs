using Microsoft.Extensions.Options;

namespace CouponService.Api.Options;

public sealed class CouponServiceOptionsValidator : IValidateOptions<CouponServiceOptions>
{
    public ValidateOptionsResult Validate(string? name, CouponServiceOptions options)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(options.LocalTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return ValidateOptionsResult.Fail(
                $"CouponService:LocalTimeZoneId '{options.LocalTimeZoneId}' is not a known time zone id.");
        }
        catch (InvalidTimeZoneException)
        {
            return ValidateOptionsResult.Fail(
                $"CouponService:LocalTimeZoneId '{options.LocalTimeZoneId}' is not a valid time zone id.");
        }

        return ValidateOptionsResult.Success;
    }
}
