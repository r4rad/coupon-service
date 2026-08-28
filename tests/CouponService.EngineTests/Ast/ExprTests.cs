using System.Collections.Immutable;
using CouponService.Engine.Ast;

namespace CouponService.EngineTests.Ast;

public sealed class ExprTests
{
    [Fact]
    public void Ast_supports_all_condition_node_types_required_by_ac_2_1()
    {
        Expr[] nodes =
        [
            new ConstExpr(Value.Of(25.00m)),
            new FactExpr("cart.subtotal"),
            new LogicalExpr(
                LogicalOp.All,
                ImmutableArray.Create<Expr>(
                    new CompareExpr(
                        CompareOp.Gte,
                        new FactExpr("cart.subtotal"),
                        new ConstExpr(Value.Of(25.00m))),
                    new QuantifierExpr(
                        QuantifierOp.Every,
                        "cart.lines",
                        new CompareExpr(
                            CompareOp.Eq,
                            new FactExpr("line.category"),
                            new ConstExpr(Value.Of("Vegetarian")))))),
            new CompareExpr(
                CompareOp.Eq,
                new FactExpr("customer.confirmedOrderCount"),
                new ConstExpr(Value.Of(0m))),
            new MembershipExpr(
                MembershipOp.In,
                new FactExpr("time.localDayOfWeek"),
                ImmutableArray.Create<Expr>(
                    new ConstExpr(Value.Of("Saturday")),
                    new ConstExpr(Value.Of("Sunday")))),
            new QuantifierExpr(
                QuantifierOp.Every,
                "cart.lines",
                new CompareExpr(
                    CompareOp.Eq,
                    new FactExpr("line.category"),
                    new ConstExpr(Value.Of("Vegetarian")))),
            new AggregateExpr(
                AggregateOp.Sum,
                new Selector(
                    new CompareExpr(
                        CompareOp.Eq,
                        new FactExpr("line.category"),
                        new ConstExpr(Value.Of("Vegetarian"))))),
            new ArithmeticExpr(
                ArithmeticOp.Add,
                ImmutableArray.Create<Expr>(
                    new ConstExpr(Value.Of(9.50m)),
                    new ConstExpr(Value.Of(12.00m)))),
        ];

        Assert.Collection(
            nodes,
            node => Assert.IsType<ConstExpr>(node),
            node => Assert.IsType<FactExpr>(node),
            node => Assert.IsType<LogicalExpr>(node),
            node => Assert.IsType<CompareExpr>(node),
            node => Assert.IsType<MembershipExpr>(node),
            node => Assert.IsType<QuantifierExpr>(node),
            node => Assert.IsType<AggregateExpr>(node),
            node => Assert.IsType<ArithmeticExpr>(node));
    }
}
