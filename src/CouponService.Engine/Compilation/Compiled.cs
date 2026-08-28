using CouponService.Engine.Evaluation;

namespace CouponService.Engine.Compilation;

public delegate ValueTask<Ast.Value> Compiled(EvalScope scope, CancellationToken cancellationToken);
