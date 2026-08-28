using CouponService.Engine.Ast;
using CouponService.Engine.Evaluation;

namespace CouponService.Engine.Facts;

public sealed class FactRegistryBuilder
{
    private readonly Dictionary<string, FactDescriptor> _facts = new(StringComparer.Ordinal);

    public FactRegistryBuilder Register(FactDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Path);

        if (!_facts.TryAdd(descriptor.Path, descriptor))
        {
            throw new DuplicateFactRegistrationException(descriptor.Path);
        }

        return this;
    }

    public FactRegistry Build() => new(_facts);
}

public sealed class FactRegistry : IFactRegistry
{
    private readonly IReadOnlyDictionary<string, FactDescriptor> _facts;

    internal FactRegistry(IReadOnlyDictionary<string, FactDescriptor> facts) =>
        _facts = facts;

    public IReadOnlyList<FactDescriptor> All => _facts.Values.OrderBy(fact => fact.Path, StringComparer.Ordinal).ToArray();

    public bool TryGet(string path, out FactDescriptor descriptor) =>
        _facts.TryGetValue(path, out descriptor!);

    public ValueTask<Value> ResolveAsync(string path, EvalScope scope, CancellationToken cancellationToken)
    {
        if (!_facts.TryGetValue(path, out var descriptor))
        {
            throw new UnknownFactException(path);
        }

        return descriptor.Resolve(scope, cancellationToken);
    }
}
