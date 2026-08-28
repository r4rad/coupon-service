using System.Collections.Immutable;
using System.Globalization;
using CouponService.Domain;
using FsCheck;

namespace CouponService.EngineTests.Properties;

internal static class PricingGenerators
{
    private static readonly string[] Categories = ["Vegetarian", "Meat", "Special"];

    internal static Gen<Cart> CartGen =>
        from lineCount in Gen.Choose(0, 5)
        from lines in Gen.ListOf(lineCount, LineGen)
        select new Cart([.. lines]);

    internal static Gen<string> EffectJsonGen =>
        EffectGen(maxDepth: 3);

    private static Gen<CartLine> LineGen =>
        from index in Gen.Choose(1, 999)
        from category in Gen.Elements(Categories)
        from unitCents in Gen.Choose(1, 10_000)
        from quantity in Gen.Choose(1, 5)
        select new CartLine(
            $"line-{index}",
            $"pizza-{index}",
            category,
            unitCents / 100m,
            quantity);

    private static Gen<string> EffectGen(int maxDepth) =>
        maxDepth <= 0
            ? LeafEffectGen
            : Gen.OneOf(
                LeafEffectGen,
                BestOfGen(maxDepth - 1),
                SumGen(maxDepth - 1),
                CapGen(maxDepth - 1));

    private static Gen<string> LeafEffectGen =>
        Gen.OneOf(
            PercentageGen,
            FixedAmountGen,
            CheapestFreeGen,
            NthItemGen,
            TieredGen);

    private static Gen<string> PercentageGen =>
        from percentage in Gen.Choose(0, 100)
        from category in Gen.Elements(Categories)
        select $$"""
            {
              "percentage": {
                "value": {{percentage.ToString(CultureInfo.InvariantCulture)}},
                "of": {{SelectorJson(category)}}
              }
            }
            """;

    private static Gen<string> FixedAmountGen =>
        from amountCents in Gen.Choose(0, 20_000)
        select $$"""
            {
              "fixedAmount": {
                "amount": {{FormatMoney(amountCents)}}
              }
            }
            """;

    private static Gen<string> CheapestFreeGen =>
        from count in Gen.Choose(0, 5)
        from category in Gen.Elements(Categories)
        select $$"""
            {
              "cheapestFree": {
                "count": {{count.ToString(CultureInfo.InvariantCulture)}},
                "from": {{SelectorJson(category)}}
              }
            }
            """;

    private static Gen<string> NthItemGen =>
        from interval in Gen.Choose(1, 5)
        from percentage in Gen.Choose(0, 100)
        from category in Gen.Elements(Categories)
        select $$"""
            {
              "nthItem": {
                "n": {{interval.ToString(CultureInfo.InvariantCulture)}},
                "percentage": {{percentage.ToString(CultureInfo.InvariantCulture)}},
                "from": {{SelectorJson(category)}}
              }
            }
            """;

    private static Gen<string> TieredGen =>
        from tierCount in Gen.Choose(1, 3)
        from thresholds in Gen.ArrayOf(tierCount, Gen.Choose(0, 20_000))
        from percentages in Gen.ArrayOf(tierCount, Gen.Choose(0, 100))
        select $$"""
            {
              "tiered": {
                "on": { "fact": "cart.subtotal" },
                "tiers": [{{string.Join(",", thresholds.Zip(percentages, TierEntry))}}]
              }
            }
            """;

    private static Gen<string> BestOfGen(int childDepth) =>
        from left in EffectGen(childDepth)
        from right in EffectGen(childDepth)
        select $$"""
            {
              "bestOf": [
                {{left}},
                {{right}}
              ]
            }
            """;

    private static Gen<string> SumGen(int childDepth) =>
        from left in EffectGen(childDepth)
        from right in EffectGen(childDepth)
        select $$"""
            {
              "sum": [
                {{left}},
                {{right}}
              ]
            }
            """;

    private static Gen<string> CapGen(int childDepth) =>
        from maxCents in Gen.Choose(0, 20_000)
        from nested in EffectGen(childDepth)
        select $$"""
            {
              "cap": {
                "max": {{FormatMoney(maxCents)}},
                "of": {{nested}}
              }
            }
            """;

    private static string SelectorJson(string category) =>
        $$"""
        {
          "lines": {
            "where": {
              "eq": [
                { "fact": "line.category" },
                "{{category}}"
              ]
            }
          }
        }
        """;

    private static string TierEntry(int thresholdCents, int percentage) =>
        $$"""
        { "from": {{FormatMoney(thresholdCents)}}, "percentage": {{percentage.ToString(CultureInfo.InvariantCulture)}} }
        """;

    private static string FormatMoney(int cents) =>
        (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}

public sealed record EffectDocument(string Json)
{
    public override string ToString() => Json;
}
