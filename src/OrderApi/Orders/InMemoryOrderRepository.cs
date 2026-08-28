using System.Collections.Concurrent;

namespace OrderApi.Orders;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, OrderRecord> _orders = new(StringComparer.OrdinalIgnoreCase);

    public Func<OrderRecord, CancellationToken, Task>? SaveInterceptor { get; set; }

    public Task SaveAsync(OrderRecord order, CancellationToken cancellationToken = default)
    {
        if (SaveInterceptor is not null)
        {
            return SaveInterceptor(order, cancellationToken);
        }

        _orders[order.OrderId] = order;
        return Task.CompletedTask;
    }

    public Task<OrderRecord?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_orders.TryGetValue(orderId, out var order) ? order : null);
}
