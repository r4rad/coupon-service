using CouponService.Application.Pricing;
using CouponService.Application.Redemption;
using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.Infrastructure.Logging;

public sealed class ObservabilityCouponRedeemer(ICouponRedeemer inner, IDomainEventLogger events) : ICouponRedeemer
{
    public async Task<ReservationResult> ReserveAsync(
        string code,
        string orderId,
        Cart cart,
        CustomerContext customer,
        CancellationToken cancellationToken = default)
    {
        var result = await inner
            .ReserveAsync(code, orderId, cart, customer, cancellationToken)
            .ConfigureAwait(false);

        var context = new DomainEventContext(
            CouponCode: code,
            OrderId: orderId,
            PolicyContentHash: result.Redemption?.PolicyContentHash,
            UserId: customer.CustomerId);

        if (result is { Succeeded: true, Redemption: not null })
        {
            events.Log(
                DomainEventNames.ReservationCreated,
                context with { PolicyContentHash = result.Redemption.PolicyContentHash });
        }
        else if (result.Reason is RejectionReason.UsageLimitReached)
        {
            events.Log(DomainEventNames.UsageLimitReached, context);
        }

        if (result.Redemption?.State is RedemptionState.Expired)
        {
            events.Log(
                DomainEventNames.ReservationExpired,
                context with { PolicyContentHash = result.Redemption.PolicyContentHash });
        }

        return result;
    }

    public async Task<RedemptionResult> ConfirmAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var result = await inner.ConfirmAsync(orderId, cancellationToken).ConfigureAwait(false);
        LogTransition(orderId, result);
        return result;
    }

    public async Task<RedemptionResult> ReleaseAsync(
        string orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var result = await inner.ReleaseAsync(orderId, reason, cancellationToken).ConfigureAwait(false);
        LogTransition(orderId, result);
        return result;
    }

    private void LogTransition(string orderId, RedemptionResult result)
    {
        if (result.Redemption is null)
        {
            return;
        }

        var context = new DomainEventContext(
            OrderId: orderId,
            PolicyContentHash: result.Redemption.PolicyContentHash,
            UserId: result.Redemption.CustomerId);

        switch (result.Redemption.State)
        {
            case RedemptionState.Confirmed when result.Succeeded:
                events.Log(DomainEventNames.RedemptionConfirmed, context);
                break;
            case RedemptionState.Released when result.Succeeded:
                events.Log(DomainEventNames.ReservationReleased, context);
                break;
            case RedemptionState.Expired:
                events.Log(DomainEventNames.ReservationExpired, context);
                break;
        }
    }
}
