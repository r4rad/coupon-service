namespace CouponService.Engine.Facts;

public interface IFactRegistry
{
    IReadOnlyList<FactDescriptor> All { get; }

    bool TryGet(string path, out FactDescriptor descriptor);

    ValueTask<Ast.Value> ResolveAsync(string path, EvalScope scope, CancellationToken cancellationToken);
}
