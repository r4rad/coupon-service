using System.Collections.Immutable;
using CouponService.Domain;

namespace CouponService.Engine.Effects;

internal static class EffectUnitExpander
{
    internal readonly record struct Unit(string LineId, decimal UnitPrice, int UnitIndex);

    internal static IEnumerable<Unit> Expand(ImmutableArray<CartLine> lines)
    {
        foreach (var line in lines)
        {
            for (var unitIndex = 0; unitIndex < line.Quantity; unitIndex++)
            {
                yield return new Unit(line.LineId, line.UnitPrice, unitIndex);
            }
        }
    }
}
