using System.Collections.Immutable;
using CouponService.Domain;

namespace CouponService.Engine.Effects;

internal static class DiscountPlanBuilder
{
    public static DiscountPlan Empty { get; } =
        new(0m, ImmutableArray<LineAllocation>.Empty);

    public static DiscountPlan AllocateProportionally(
        IReadOnlyList<(string LineId, decimal Weight)> lines,
        decimal targetTotal)
    {
        var roundedTotal = Money.Round(targetTotal);
        if (roundedTotal <= 0 || lines.Count == 0)
        {
            return Empty;
        }

        var weightSum = lines.Sum(line => line.Weight);
        if (weightSum <= 0)
        {
            return Empty;
        }

        return DistributeWithRemainder(lines, roundedTotal, weightSum);
    }

    public static DiscountPlan RescaleToMaximum(DiscountPlan plan, decimal maximum)
    {
        var cappedMaximum = Money.Round(maximum);
        if (plan.Total <= cappedMaximum)
        {
            return plan;
        }

        if (plan.Allocations.IsEmpty)
        {
            return Empty;
        }

        var weights = plan.Allocations
            .Select(allocation => (allocation.LineId, allocation.Amount))
            .ToArray();

        return DistributeWithRemainder(weights, cappedMaximum, plan.Total);
    }

    public static DiscountPlan SumPlans(IReadOnlyList<DiscountPlan> plans)
    {
        if (plans.Count == 0)
        {
            return Empty;
        }

        var totalsByLine = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            foreach (var allocation in plan.Allocations)
            {
                totalsByLine[allocation.LineId] =
                    totalsByLine.GetValueOrDefault(allocation.LineId) + allocation.Amount;
            }
        }

        var allocations = totalsByLine
            .Select(pair => new LineAllocation(pair.Key, Money.Round(pair.Value)))
            .Where(allocation => allocation.Amount > 0)
            .ToImmutableArray();

        var total = Money.Round(allocations.Sum(allocation => allocation.Amount));
        return new DiscountPlan(total, allocations);
    }

    public static DiscountPlan SelectBest(IReadOnlyList<DiscountPlan> plans)
    {
        if (plans.Count == 0)
        {
            return Empty;
        }

        return plans.MaxBy(plan => plan.Total) ?? Empty;
    }

    private static DiscountPlan DistributeWithRemainder(
        IReadOnlyList<(string LineId, decimal Weight)> lines,
        decimal targetTotal,
        decimal weightSum)
    {
        var entries = new List<(string LineId, decimal Rounded, decimal Fraction)>(lines.Count);
        foreach (var (lineId, weight) in lines)
        {
            var unrounded = weight * targetTotal / weightSum;
            var rounded = Money.Round(unrounded);
            entries.Add((lineId, rounded, unrounded - rounded));
        }

        var sum = entries.Sum(entry => entry.Rounded);
        var remainderCents = (int)decimal.Round(
            (targetTotal - sum) * 100m,
            0,
            MidpointRounding.AwayFromZero);

        if (remainderCents != 0)
        {
            var sorted = remainderCents > 0
                ? entries.OrderByDescending(entry => entry.Fraction).ToList()
                : entries.OrderBy(entry => entry.Fraction).ToList();

            var adjustment = remainderCents > 0 ? 0.01m : -0.01m;
            var adjustments = Math.Abs(remainderCents);
            for (var index = 0; index < adjustments && index < sorted.Count; index++)
            {
                var lineId = sorted[index].LineId;
                var entryIndex = entries.FindIndex(entry => entry.LineId == lineId);
                var entry = entries[entryIndex];
                entries[entryIndex] = (entry.LineId, entry.Rounded + adjustment, entry.Fraction);
            }
        }

        var allocations = entries
            .Where(entry => entry.Rounded > 0)
            .Select(entry => new LineAllocation(entry.LineId, entry.Rounded))
            .ToImmutableArray();

        var total = Money.Round(allocations.Sum(allocation => allocation.Amount));
        return new DiscountPlan(total, allocations);
    }
}
