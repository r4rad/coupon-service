using CouponService.Domain;
using CouponService.Engine.Ast;
using CouponService.Engine.Evaluation;

namespace CouponService.Engine.Facts;

public static class StandardFactVocabulary
{
    public static FactRegistry Create() =>
        new FactRegistryBuilder()
            .Register(CreateCartSubtotal())
            .Register(CreateCartLineCount())
            .Register(CreateCartTotalQuantity())
            .Register(CreateLineCategory())
            .Register(CreateLineUnitPrice())
            .Register(CreateLineQuantity())
            .Register(CreateCustomerConfirmedOrderCount())
            .Register(CreateCustomerIsFirstOrder())
            .Register(CreateCouponUsesTotal())
            .Register(CreateCouponUsesByCustomer())
            .Register(CreateTimeUtcNow())
            .Register(CreateTimeLocalDayOfWeek())
            .Register(CreateTimeLocalHour())
            .Build();

    private static FactDescriptor CreateCartSubtotal() =>
        new(
            "cart.subtotal",
            ValueKind.Number,
            FactCost.Pure,
            static (scope, _) => ValueTask.FromResult(Value.Of(scope.Cart.Subtotal)));

    private static FactDescriptor CreateCartLineCount() =>
        new(
            "cart.lineCount",
            ValueKind.Number,
            FactCost.Pure,
            static (scope, _) => ValueTask.FromResult(Value.Of(scope.Cart.Lines.Length)));

    private static FactDescriptor CreateCartTotalQuantity() =>
        new(
            "cart.totalQuantity",
            ValueKind.Number,
            FactCost.Pure,
            static (scope, _) => ValueTask.FromResult(Value.Of(scope.Cart.Lines.Sum(line => line.Quantity))));

    private static FactDescriptor CreateLineCategory() =>
        new(
            "line.category",
            ValueKind.Text,
            FactCost.Pure,
            static (scope, _) => ValueTask.FromResult(Value.Of(RequireCurrentLine(scope).Category)));

    private static FactDescriptor CreateLineUnitPrice() =>
        new(
            "line.unitPrice",
            ValueKind.Number,
            FactCost.Pure,
            static (scope, _) => ValueTask.FromResult(Value.Of(RequireCurrentLine(scope).UnitPrice)));

    private static FactDescriptor CreateLineQuantity() =>
        new(
            "line.quantity",
            ValueKind.Number,
            FactCost.Pure,
            static (scope, _) => ValueTask.FromResult(Value.Of(RequireCurrentLine(scope).Quantity)));

    private static FactDescriptor CreateCustomerConfirmedOrderCount() =>
        new(
            "customer.confirmedOrderCount",
            ValueKind.Number,
            FactCost.Cached,
            static (scope, _) => ValueTask.FromResult(Value.Of(RequireConfirmedOrderCount(scope))));

    private static FactDescriptor CreateCustomerIsFirstOrder() =>
        new(
            "customer.isFirstOrder",
            ValueKind.Bool,
            FactCost.Cached,
            static (scope, _) => ValueTask.FromResult(Value.Of(RequireIsFirstOrder(scope))));

    private static FactDescriptor CreateCouponUsesTotal() =>
        new(
            "coupon.uses.total",
            ValueKind.Number,
            FactCost.RemoteRead,
            static (scope, _) => ValueTask.FromResult(Value.Of(RequireCouponUsesTotal(scope))));

    private static FactDescriptor CreateCouponUsesByCustomer() =>
        new(
            "coupon.uses.byCustomer",
            ValueKind.Number,
            FactCost.RemoteRead,
            static (scope, _) => ValueTask.FromResult(Value.Of(RequireCouponUsesByCustomer(scope))));

    private static FactDescriptor CreateTimeUtcNow() =>
        new(
            "time.utcNow",
            ValueKind.Number,
            FactCost.Pure,
            static (scope, _) => ValueTask.FromResult(Value.Of(scope.Clock.UtcNow.ToUnixTimeSeconds())));

    private static FactDescriptor CreateTimeLocalDayOfWeek() =>
        new(
            "time.localDayOfWeek",
            ValueKind.Text,
            FactCost.Pure,
            static (scope, _) => ValueTask.FromResult(Value.Of(scope.Clock.UtcNow.DayOfWeek.ToString())));

    private static FactDescriptor CreateTimeLocalHour() =>
        new(
            "time.localHour",
            ValueKind.Number,
            FactCost.Pure,
            static (scope, _) => ValueTask.FromResult(Value.Of(scope.Clock.UtcNow.Hour)));

    private static CartLine RequireCurrentLine(EvalScope scope) =>
        scope.CurrentLine ?? throw new InvalidOperationException("line.* facts require quantifier scope.");

    private static int RequireConfirmedOrderCount(EvalScope scope) =>
        scope.ConfirmedOrderCount ?? throw new InvalidOperationException("customer.confirmedOrderCount is unavailable.");

    private static bool RequireIsFirstOrder(EvalScope scope) =>
        scope.IsFirstOrder ?? throw new InvalidOperationException("customer.isFirstOrder is unavailable.");

    private static int RequireCouponUsesTotal(EvalScope scope) =>
        scope.CouponUsesTotal ?? throw new InvalidOperationException("coupon.uses.total is unavailable.");

    private static int RequireCouponUsesByCustomer(EvalScope scope) =>
        scope.CouponUsesByCustomer ?? throw new InvalidOperationException("coupon.uses.byCustomer is unavailable.");
}
