using System.Collections.Immutable;

namespace CouponService.Engine.Ast;

public abstract record Expr;

public sealed record ConstExpr(Value Value) : Expr;

public sealed record FactExpr(string Path) : Expr;

public sealed record LogicalExpr(LogicalOp Op, ImmutableArray<Expr> Operands) : Expr;

public sealed record CompareExpr(CompareOp Op, Expr Left, Expr Right) : Expr;

public sealed record MembershipExpr(MembershipOp Op, Expr Subject, ImmutableArray<Expr> Set) : Expr;

public sealed record QuantifierExpr(QuantifierOp Op, string Over, Expr Where) : Expr;

public sealed record AggregateExpr(AggregateOp Op, Selector Over) : Expr;

public sealed record ArithmeticExpr(ArithmeticOp Op, ImmutableArray<Expr> Operands) : Expr;
