using CouponService.Domain;
using CouponService.Engine.Ast;
using CouponService.Engine.Facts;

namespace CouponService.Engine.Evaluation;

public sealed class EvalScope
{
    internal EvalScopeState State { get; init; } = null!;

    public required IClock Clock { get; init; }

    public required Cart Cart { get; init; }

    public required IFactRegistry Registry { get; init; }

    public CartLine? CurrentLine { get; init; }

    public int? ConfirmedOrderCount { get; init; }

    public bool? IsFirstOrder { get; init; }

    public int? CouponUsesTotal { get; init; }

    public int? CouponUsesByCustomer { get; init; }

    public bool CaptureFullTrace => State.CaptureFullTrace;

    public TraceCollector Trace => State.Trace;

    public static EvalScope Create(
        IClock clock,
        Cart cart,
        IFactRegistry registry,
        bool captureFullTrace = false,
        CartLine? currentLine = null,
        int? confirmedOrderCount = null,
        bool? isFirstOrder = null,
        int? couponUsesTotal = null,
        int? couponUsesByCustomer = null) =>
        new()
        {
            State = new EvalScopeState
            {
                Trace = new TraceCollector(captureFullTrace),
                CaptureFullTrace = captureFullTrace,
            },
            Clock = clock,
            Cart = cart,
            Registry = registry,
            CurrentLine = currentLine,
            ConfirmedOrderCount = confirmedOrderCount,
            IsFirstOrder = isFirstOrder,
            CouponUsesTotal = couponUsesTotal,
            CouponUsesByCustomer = couponUsesByCustomer,
        };

    public EvalScope WithCurrentLine(CartLine line) =>
        new()
        {
            State = State,
            Clock = Clock,
            Cart = Cart,
            Registry = Registry,
            CurrentLine = line,
            ConfirmedOrderCount = ConfirmedOrderCount,
            IsFirstOrder = IsFirstOrder,
            CouponUsesTotal = CouponUsesTotal,
            CouponUsesByCustomer = CouponUsesByCustomer,
        };

    public async ValueTask<Value> ResolveFactAsync(string path, CancellationToken cancellationToken)
    {
        if (State.FactMemo.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var value = await Registry.ResolveAsync(path, this, cancellationToken).ConfigureAwait(false);
        State.FactMemo[path] = value;
        return value;
    }
}
