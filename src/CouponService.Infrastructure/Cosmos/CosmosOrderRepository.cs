namespace CouponService.Infrastructure.Cosmos;

/// <summary>
/// Orders container port (P-10). OrderApi wires this later; document shape lives with the other Cosmos adapters.
/// </summary>
public interface ICosmosOrderRepository
{
    Task SaveAsync(OrderDocument order, CancellationToken cancellationToken = default);

    Task<OrderDocument?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default);
}

public sealed class CosmosOrderRepository(ICosmosItemStore store) : ICosmosOrderRepository
{
    public async Task SaveAsync(OrderDocument order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(order.OrderId);

        order.Id = order.OrderId;
        var existing = await store
            .ReadAsync<OrderDocument>(order.OrderId, order.OrderId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await store
                .CreateAsync(order, order.OrderId, order.OrderId, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await store
            .ReplaceAsync(order, order.OrderId, order.OrderId, existing.ETag, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OrderDocument?> GetByIdAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        var result = await store
            .ReadAsync<OrderDocument>(orderId, orderId, cancellationToken)
            .ConfigureAwait(false);
        return result?.Resource;
    }
}
