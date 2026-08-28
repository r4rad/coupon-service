using CouponService.Application.Policies;
using CouponService.Application.Pricing;
using CouponService.Application.Validation;
using CouponService.Domain;
using CouponService.Engine.Caching;

namespace CouponService.Application.Redemption;

public sealed class CouponRedeemer(
    ICouponValidator validator,
    IPriceCalculator calculator,
    IPolicyRepository policies,
    IRedemptionRepository redemptions,
    IClock clock) : ICouponRedeemer
{
    public async Task<ReservationResult> ReserveAsync(
        string code,
        string orderId,
        Cart cart,
        CustomerContext customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(customer);

        var policy = await policies.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            var notFoundDecision = PolicyDecision.NotFound();
            return new ReservationResult(
                false,
                RejectionReason.NotFound,
                null,
                calculator.Calculate(cart, notFoundDecision));
        }

        var existing = await redemptions.FindByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            await redemptions
                .ExpireStaleReservationsAsync(policy.PartitionKey, clock.UtcNow, cancellationToken)
                .ConfigureAwait(false);

            var existingDecision = await validator.ValidateAsync(
                    code,
                    cart,
                    customer,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new ReservationResult(
                true,
                null,
                existing,
                calculator.Calculate(cart, existingDecision));
        }

        await redemptions
            .ExpireStaleReservationsAsync(policy.PartitionKey, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        var (totalUses, perCustomer) = PolicyLimitsReader.ReadLimits(policy.DocumentJson);
        var decision = await validator.ValidateAsync(code, cart, customer, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var breakdown = calculator.Calculate(cart, decision);

        if (await IsGlobalCapReachedAsync(policy.PartitionKey, totalUses, cancellationToken).ConfigureAwait(false))
        {
            return new ReservationResult(false, RejectionReason.UsageLimitReached, null, breakdown);
        }

        if (await IsPerCustomerCapReachedAsync(
                policy.PartitionKey,
                customer.CustomerId,
                perCustomer,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return new ReservationResult(false, RejectionReason.PerCustomerLimitReached, null, breakdown);
        }

        if (decision.Status is not CouponStatus.Applied || decision.Plan is null)
        {
            return new ReservationResult(false, decision.Reason, null, breakdown);
        }

        var contentHash = decision.PolicyContentHash
            ?? PolicyContentHasher.ComputeHash(policy.DocumentJson);
        var discountApplied = decision.Plan.Total;
        var ttlExpiresAt = clock.UtcNow.Add(RedemptionConstants.ReservationTtl);

        for (var attempt = 0; attempt < RedemptionConstants.MaxRetryAttempts; attempt++)
        {
            try
            {
                await redemptions
                    .ExpireStaleReservationsAsync(policy.PartitionKey, clock.UtcNow, cancellationToken)
                    .ConfigureAwait(false);

                var idempotent = await redemptions.FindByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
                if (idempotent is not null)
                {
                    return new ReservationResult(true, null, idempotent, breakdown);
                }

                if (await IsGlobalCapReachedAsync(
                        policy.PartitionKey,
                        totalUses,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    return new ReservationResult(false, RejectionReason.UsageLimitReached, null, breakdown);
                }

                if (await IsPerCustomerCapReachedAsync(
                        policy.PartitionKey,
                        customer.CustomerId,
                        perCustomer,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    return new ReservationResult(false, RejectionReason.PerCustomerLimitReached, null, breakdown);
                }

                var counter = await redemptions
                    .GetCounterAsync(policy.PartitionKey, cancellationToken)
                    .ConfigureAwait(false)
                    ?? new UsageCounterRecord(policy.PartitionKey, 0, 0, string.Empty);

                var counterEtag = string.IsNullOrEmpty(counter.ETag) ? "\"seed\"" : counter.ETag;

                var redemption = new RedemptionRecord(
                    policy.PartitionKey,
                    orderId,
                    customer.CustomerId,
                    RedemptionState.Reserved,
                    discountApplied,
                    contentHash,
                    string.Empty,
                    ttlExpiresAt);

                var updatedCounter = counter with { ActiveReservations = counter.ActiveReservations + 1 };
                var reserved = await redemptions.TryReserveAsync(
                        redemption,
                        updatedCounter,
                        counterEtag,
                        cancellationToken)
                    .ConfigureAwait(false);

                return new ReservationResult(true, null, reserved.Redemption, breakdown);
            }
            catch (PreconditionFailedException)
            {
                if (attempt < RedemptionConstants.MaxRetryAttempts - 1)
                {
                    await DelayForRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return new ReservationResult(false, RejectionReason.UsageLimitReached, null, breakdown);
    }

    public async Task<RedemptionResult> ConfirmAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        var redemption = await redemptions.FindByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (redemption is null)
        {
            return new RedemptionResult(false, null);
        }

        await redemptions
            .ExpireStaleReservationsAsync(redemption.PartitionKey, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        redemption = await redemptions.FindByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (redemption is null)
        {
            return new RedemptionResult(false, null);
        }

        if (redemption.State is RedemptionState.Confirmed)
        {
            return new RedemptionResult(true, redemption);
        }

        if (redemption.State is not RedemptionState.Reserved)
        {
            return new RedemptionResult(false, redemption);
        }

        var counter = await redemptions
            .GetCounterAsync(redemption.PartitionKey, cancellationToken)
            .ConfigureAwait(false)
            ?? new UsageCounterRecord(redemption.PartitionKey, 0, 0, string.Empty);

        var counterEtag = string.IsNullOrEmpty(counter.ETag) ? "\"seed\"" : counter.ETag;

        var confirmedRedemption = redemption with
        {
            State = RedemptionState.Confirmed,
            TtlExpiresAt = null,
        };

        var updatedCounter = counter with
        {
            ConfirmedCount = counter.ConfirmedCount + 1,
            ActiveReservations = Math.Max(0, counter.ActiveReservations - 1),
        };

        var result = await redemptions.TryConfirmAsync(
                confirmedRedemption,
                updatedCounter,
                redemption.ETag,
                counterEtag,
                cancellationToken)
            .ConfigureAwait(false);

        return new RedemptionResult(true, result.Redemption);
    }

    public async Task<RedemptionResult> ReleaseAsync(
        string orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var redemption = await redemptions.FindByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (redemption is null)
        {
            return new RedemptionResult(false, null);
        }

        await redemptions
            .ExpireStaleReservationsAsync(redemption.PartitionKey, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        redemption = await redemptions.FindByOrderIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (redemption is null)
        {
            return new RedemptionResult(false, null);
        }

        if (redemption.State is RedemptionState.Released)
        {
            return new RedemptionResult(true, redemption);
        }

        if (redemption.State is not RedemptionState.Reserved)
        {
            return new RedemptionResult(false, redemption);
        }

        var counter = await redemptions
            .GetCounterAsync(redemption.PartitionKey, cancellationToken)
            .ConfigureAwait(false)
            ?? new UsageCounterRecord(redemption.PartitionKey, 0, 0, string.Empty);

        var counterEtag = string.IsNullOrEmpty(counter.ETag) ? "\"seed\"" : counter.ETag;

        var releasedRedemption = redemption with
        {
            State = RedemptionState.Released,
            TtlExpiresAt = null,
        };

        var updatedCounter = counter with
        {
            ActiveReservations = Math.Max(0, counter.ActiveReservations - 1),
        };

        var result = await redemptions.TryReleaseAsync(
                releasedRedemption,
                updatedCounter,
                redemption.ETag,
                counterEtag,
                cancellationToken)
            .ConfigureAwait(false);

        return new RedemptionResult(true, result.Redemption);
    }

    private async Task<bool> IsGlobalCapReachedAsync(
        string partitionKey,
        int? totalUses,
        CancellationToken cancellationToken)
    {
        if (totalUses is null)
        {
            return false;
        }

        var counter = await redemptions.GetCounterAsync(partitionKey, cancellationToken).ConfigureAwait(false);
        if (counter is null)
        {
            return false;
        }

        return counter.ConfirmedCount + counter.ActiveReservations >= totalUses.Value;
    }

    private async Task<bool> IsPerCustomerCapReachedAsync(
        string partitionKey,
        string customerId,
        int? perCustomer,
        CancellationToken cancellationToken)
    {
        if (perCustomer is null)
        {
            return false;
        }

        var customerUses = await redemptions
            .CountConsumingByCustomerAsync(partitionKey, customerId, cancellationToken)
            .ConfigureAwait(false);

        return customerUses >= perCustomer.Value;
    }

    private static async Task DelayForRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var jitter = Random.Shared.Next(0, 50);
        var delayMs = ((attempt + 1) * 10) + jitter;
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
    }
}
