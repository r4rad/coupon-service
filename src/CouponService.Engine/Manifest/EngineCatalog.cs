namespace CouponService.Engine.Manifest;

public static class EngineCatalog
{
    public static IReadOnlyList<string> ConditionOperators { get; } =
    [
        "all",
        "any",
        "not",
        "eq",
        "neq",
        "gt",
        "gte",
        "lt",
        "lte",
        "in",
        "between",
        "every",
        "some",
        "sum",
        "count",
        "min",
        "max",
        "add",
        "sub",
        "mul",
        "minOf",
        "maxOf",
    ];

    public static IReadOnlyList<string> EffectOperators { get; } =
    [
        "percentage",
        "fixedAmount",
        "cheapestFree",
        "nthItem",
        "tiered",
        "bestOf",
        "sum",
        "cap",
    ];
}
