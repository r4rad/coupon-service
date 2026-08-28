namespace CouponService.Engine.Ast;

public enum LogicalOp
{
    All,
    Any,
    Not,
}

public enum CompareOp
{
    Eq,
    Neq,
    Gt,
    Gte,
    Lt,
    Lte,
}

public enum MembershipOp
{
    In,
    Between,
}

public enum QuantifierOp
{
    Every,
    Some,
}

public enum AggregateOp
{
    Sum,
    Count,
    Min,
    Max,
}

public enum ArithmeticOp
{
    Add,
    Sub,
    Mul,
    MinOf,
    MaxOf,
}
