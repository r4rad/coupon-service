namespace OrderApi.Auth;

public interface ICouponServiceTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class ConfigurationCouponServiceTokenProvider(
    Microsoft.Extensions.Options.IOptions<OrderApi.Options.OrderApiOptions> options) : ICouponServiceTokenProvider
{
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = options.Value.CouponServiceToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "OrderApi:CouponServiceToken must be configured for calls to the Coupon Service.");
        }

        return Task.FromResult(token);
    }
}
