using CouponService.Application.Preview;
using CouponService.Application.Pricing;
using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.Infrastructure.Logging;

// AC-8.3: emit preview domain events without changing application services.
public sealed class ObservabilityCouponPreviewService(ICouponPreviewService inner, IDomainEventLogger events)
    : ICouponPreviewService
{
    public async Task<PreviewResult> PreviewAsync(
        string code,
        Cart cart,
        CustomerContext customer,
        CancellationToken cancellationToken = default)
    {
        var result = await inner
            .PreviewAsync(code, cart, customer, cancellationToken)
            .ConfigureAwait(false);

        var context = new DomainEventContext(
            CouponCode: code,
            PolicyContentHash: result.Decision.PolicyContentHash,
            UserId: customer.CustomerId);

        events.Log(DomainEventNames.CouponPreviewed, context);

        if (result.Decision.Status is CouponStatus.Applied)
        {
            events.Log(DomainEventNames.CouponApplied, context);
        }
        else
        {
            events.Log(DomainEventNames.CouponRejected, context);
        }

        return result;
    }
}
