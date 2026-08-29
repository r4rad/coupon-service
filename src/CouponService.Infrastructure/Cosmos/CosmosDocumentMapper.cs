using CouponService.Application.Policies;
using CouponService.Application.Redemption;

namespace CouponService.Infrastructure.Cosmos;

internal static class CosmosDocumentMapper
{
    /// <summary>Matches Application reservation TTL (900s) — that constant is internal to Application.</summary>
    public const int ReservationTtlSeconds = 900;

    internal static PolicyDocument ToDocument(PolicyRecord policy)
    {
        var status = PolicyDocumentMetadata.ReadStatus(policy.DocumentJson).ToString();
        return new PolicyDocument
        {
            Id = policy.PolicyId,
            Pk = policy.PartitionKey,
            PolicyId = policy.PolicyId,
            Code = policy.Code,
            Trigger = policy.Trigger.ToString(),
            Status = status,
            DocumentJson = policy.DocumentJson,
        };
    }

    internal static PolicyRecord ToRecord(PolicyDocument document, string etag) =>
        new(
            document.Pk,
            document.PolicyId,
            document.Code,
            ParseTrigger(document.Trigger),
            document.DocumentJson,
            etag);

    internal static CounterDocument ToDocument(UsageCounterRecord counter) =>
        new()
        {
            Id = CounterDocument.DocumentId,
            Pk = counter.PartitionKey,
            Type = CounterDocument.DocumentType,
            ConfirmedCount = counter.ConfirmedCount,
            ActiveReservations = counter.ActiveReservations,
        };

    internal static UsageCounterRecord ToRecord(CounterDocument document, string etag) =>
        new(document.Pk, document.ConfirmedCount, document.ActiveReservations, etag);

    internal static RedemptionDocument ToDocument(RedemptionRecord redemption)
    {
        // Cosmos TTL is relative seconds from last write; absolute expiry stays in ttlExpiresAt for app-side expire.
        int? ttl = redemption.State is RedemptionState.Reserved
            ? ReservationTtlSeconds
            : null;

        return new RedemptionDocument
        {
            Id = redemption.OrderId,
            Pk = redemption.PartitionKey,
            Type = RedemptionDocument.DocumentType,
            OrderId = redemption.OrderId,
            CustomerId = redemption.CustomerId,
            State = redemption.State.ToString(),
            DiscountApplied = redemption.DiscountApplied,
            PolicyContentHash = redemption.PolicyContentHash,
            Ttl = ttl,
            TtlExpiresAt = redemption.State is RedemptionState.Reserved ? redemption.TtlExpiresAt : null,
        };
    }

    internal static RedemptionRecord ToRecord(RedemptionDocument document, string etag) =>
        new(
            document.Pk,
            document.OrderId,
            document.CustomerId,
            Enum.Parse<RedemptionState>(document.State, ignoreCase: true),
            document.DiscountApplied,
            document.PolicyContentHash,
            etag,
            document.TtlExpiresAt);

    private static PolicyTrigger ParseTrigger(string trigger) =>
        string.Equals(trigger, nameof(PolicyTrigger.Automatic), StringComparison.OrdinalIgnoreCase)
            ? PolicyTrigger.Automatic
            : PolicyTrigger.Code;
}
