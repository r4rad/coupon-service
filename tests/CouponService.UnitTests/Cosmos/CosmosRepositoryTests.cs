using System.Collections.Concurrent;
using CouponService.Application.Policies;
using CouponService.Application.Redemption;
using CouponService.Infrastructure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CouponService.UnitTests.CosmosAdapter;

public sealed class CosmosRedemptionRepositoryTests
{
    [Fact]
    public async Task Concurrent_TryReserve_at_last_use_grants_exactly_one_and_412_loser()
    {
        var store = new InMemoryCosmosItemStore();
        var repository = new CosmosRedemptionRepository(store, NullLogger<CosmosRedemptionRepository>.Instance);

        await repository.UpsertCounterAsync(
            new UsageCounterRecord("LIMITED1", ConfirmedCount: 0, ActiveReservations: 0, ETag: string.Empty),
            "\"seed\"");

        var counter = await repository.GetCounterAsync("LIMITED1");
        Assert.NotNull(counter);

        var redemptionA = NewReserved("LIMITED1", "order-a", "customer-a");
        var redemptionB = NewReserved("LIMITED1", "order-b", "customer-b");
        var nextCounter = counter! with { ActiveReservations = 1 };

        var results = await Task.WhenAll(
            Wrap(repository.TryReserveAsync(redemptionA, nextCounter, counter.ETag)),
            Wrap(repository.TryReserveAsync(redemptionB, nextCounter, counter.ETag)));

        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.Equal(1, results.Count(r => r.PreconditionFailed));

        var after = await repository.GetCounterAsync("LIMITED1");
        Assert.Equal(1, after!.ActiveReservations);
    }

    [Fact]
    public async Task Duplicate_orderId_on_reserve_throws_without_incrementing_counter()
    {
        var store = new InMemoryCosmosItemStore();
        var repository = new CosmosRedemptionRepository(store, NullLogger<CosmosRedemptionRepository>.Instance);

        await repository.UpsertCounterAsync(
            new UsageCounterRecord("SAVE10", 0, 0, string.Empty),
            "\"seed\"");
        var counter = (await repository.GetCounterAsync("SAVE10"))!;

        var first = NewReserved("SAVE10", "order-same", "customer-1");
        await repository.TryReserveAsync(first, counter with { ActiveReservations = 1 }, counter.ETag);

        var afterFirst = (await repository.GetCounterAsync("SAVE10"))!;
        var duplicate = NewReserved("SAVE10", "order-same", "customer-2");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.TryReserveAsync(
                duplicate,
                afterFirst with { ActiveReservations = 2 },
                afterFirst.ETag));

        var after = await repository.GetCounterAsync("SAVE10");
        Assert.Equal(1, after!.ActiveReservations);
        Assert.Equal(afterFirst.ETag, after.ETag);
    }

    [Fact]
    public async Task Reserved_redemption_document_sets_ttl_and_confirm_clears_it()
    {
        var store = new InMemoryCosmosItemStore();
        var repository = new CosmosRedemptionRepository(store, NullLogger<CosmosRedemptionRepository>.Instance);

        await repository.UpsertCounterAsync(
            new UsageCounterRecord("SAVE10", 0, 0, string.Empty),
            "\"seed\"");
        var counter = (await repository.GetCounterAsync("SAVE10"))!;

        var expires = new DateTimeOffset(2026, 8, 28, 15, 15, 0, TimeSpan.Zero);
        var reserved = NewReserved("SAVE10", "order-ttl", "customer-1") with { TtlExpiresAt = expires };
        var (redemption, reservedCounter) = await repository.TryReserveAsync(
            reserved,
            counter with { ActiveReservations = 1 },
            counter.ETag);

        var stored = await store.ReadAsync<RedemptionDocument>("order-ttl", "SAVE10");
        Assert.NotNull(stored);
        Assert.Equal(900, stored!.Resource.Ttl);
        Assert.Equal(expires, stored.Resource.TtlExpiresAt);

        var confirmed = redemption with
        {
            State = RedemptionState.Confirmed,
            TtlExpiresAt = null,
        };
        await repository.TryConfirmAsync(
            confirmed,
            reservedCounter with { ConfirmedCount = 1, ActiveReservations = 0 },
            redemption.ETag,
            reservedCounter.ETag);

        var afterConfirm = await store.ReadAsync<RedemptionDocument>("order-ttl", "SAVE10");
        Assert.NotNull(afterConfirm);
        Assert.Null(afterConfirm!.Resource.Ttl);
        Assert.Null(afterConfirm.Resource.TtlExpiresAt);
        Assert.Equal(nameof(RedemptionState.Confirmed), afterConfirm.Resource.State);
    }

    private static RedemptionRecord NewReserved(string pk, string orderId, string customerId) =>
        new(
            pk,
            orderId,
            customerId,
            RedemptionState.Reserved,
            DiscountApplied: 3.10m,
            PolicyContentHash: "hash",
            ETag: string.Empty,
            TtlExpiresAt: new DateTimeOffset(2026, 8, 28, 15, 15, 0, TimeSpan.Zero));

    private static async Task<(bool Succeeded, bool PreconditionFailed)> Wrap(
        Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> task)
    {
        try
        {
            await task.ConfigureAwait(false);
            return (true, false);
        }
        catch (PreconditionFailedException)
        {
            return (false, true);
        }
    }
}

public sealed class CosmosRequestChargeLoggingTests
{
    [Fact]
    public async Task Store_operations_log_RequestCharge_as_structured_field()
    {
        var logger = new CapturingLogger();
        var store = new InMemoryCosmosItemStore(logger) { NextRequestCharge = 3.7 };
        var repository = new CosmosRedemptionRepository(
            store,
            NullLogger<CosmosRedemptionRepository>.Instance);

        await repository.UpsertCounterAsync(
            new UsageCounterRecord("SAVE10", 0, 0, string.Empty),
            "\"seed\"");

        Assert.Contains(
            logger.Messages,
            message => message.Contains("RequestCharge", StringComparison.Ordinal)
                && message.Contains("3.7", StringComparison.Ordinal));
    }

    private sealed class CapturingLogger : ILogger
    {
        internal ConcurrentBag<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}

public sealed class CosmosPolicyRepositoryTests
{
    [Fact]
    public async Task Create_and_get_by_code_round_trips_policy_under_pk_convention()
    {
        var store = new InMemoryCosmosItemStore();
        var repository = new CosmosPolicyRepository(store);

        var created = await repository.CreateAsync(
            new PolicyRecord(
                PartitionKey: "SAVE10",
                PolicyId: "save10",
                Code: "SAVE10",
                Trigger: PolicyTrigger.Code,
                DocumentJson: """{"policyId":"save10","code":"SAVE10","trigger":"code","status":"Active","engineSchema":"1.0","condition":{"gte":[{"fact":"cart.subtotal"},0]},"effect":{"fixedAmount":{"amount":1}}}""",
                ETag: string.Empty));

        Assert.False(string.IsNullOrWhiteSpace(created.ETag));

        var loaded = await repository.GetByCodeAsync("SAVE10");
        Assert.NotNull(loaded);
        Assert.Equal("save10", loaded!.PolicyId);
        Assert.Equal("SAVE10", loaded.PartitionKey);
    }
}

public sealed class CosmosOrderRepositoryTests
{
    [Fact]
    public async Task Save_and_get_persist_order_partitioned_by_orderId()
    {
        var store = new InMemoryCosmosItemStore();
        var repository = new CosmosOrderRepository(store);

        var order = new OrderDocument
        {
            OrderId = "order-1",
            CustomerId = "customer-1",
            Currency = "EUR",
            Subtotal = 31.00m,
            Discount = 3.10m,
            Total = 27.90m,
            CouponCode = "SAVE10",
            Lines =
            [
                new OrderLineDocument
                {
                    LineId = "line-1",
                    PizzaId = "margherita",
                    Category = "classic",
                    UnitPrice = 9.50m,
                    Quantity = 2,
                    LineTotal = 19.00m,
                },
            ],
            CreatedAt = new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero),
        };

        await repository.SaveAsync(order);
        var loaded = await repository.GetByIdAsync("order-1");
        Assert.NotNull(loaded);
        Assert.Equal(27.90m, loaded!.Total);
        Assert.Equal("SAVE10", loaded.CouponCode);
    }
}
