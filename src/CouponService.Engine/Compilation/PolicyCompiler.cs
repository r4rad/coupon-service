using CouponService.Domain;
using CouponService.Engine.Ast;
using CouponService.Engine.Evaluation;
using CouponService.Engine.Facts;
using CouponService.Engine.Validation;

namespace CouponService.Engine.Compilation;

public sealed class PolicyCompiler
{
    public CompiledCondition Compile(Expr condition, IFactRegistry registry)
    {
        var part = CompileExpr(condition, PolicyValidator.ConditionPath, registry, inLineScope: false);
        return new CompiledCondition(part.Compiled, part.MaxCost);
    }

    private static CompiledPart CompileExpr(
        Expr expression,
        string path,
        IFactRegistry registry,
        bool inLineScope)
    {
        return expression switch
        {
            ConstExpr constant => CompileConst(constant),
            FactExpr fact => CompileFact(fact, path, registry),
            LogicalExpr logical => CompileLogical(logical, path, registry, inLineScope),
            CompareExpr compare => CompileCompare(compare, path, registry, inLineScope),
            MembershipExpr membership => CompileMembership(membership, path, registry, inLineScope),
            QuantifierExpr quantifier => CompileQuantifier(quantifier, path, registry),
            AggregateExpr aggregate => CompileAggregate(aggregate, path, registry),
            ArithmeticExpr arithmetic => CompileArithmetic(arithmetic, path, registry, inLineScope),
            _ => throw new ArgumentOutOfRangeException(nameof(expression)),
        };
    }

    private static CompiledPart CompileConst(ConstExpr constant)
    {
        var value = constant.Value;
        return new CompiledPart(
            (_, _) => ValueTask.FromResult(value),
            FactCost.Pure);
    }

    private static CompiledPart CompileFact(FactExpr fact, string path, IFactRegistry registry)
    {
        if (!registry.TryGet(fact.Path, out var descriptor))
        {
            throw new UnknownFactException(fact.Path);
        }

        var factPath = CompilePaths.FactPath(path);

        return new CompiledPart(
            async (scope, cancellationToken) =>
            {
                var value = await scope.ResolveFactAsync(fact.Path, cancellationToken).ConfigureAwait(false);
                RecordNode(scope, factPath, "fact", value, passed: null);
                return value;
            },
            descriptor.Cost);
    }

    private static CompiledPart CompileLogical(
        LogicalExpr logical,
        string path,
        IFactRegistry registry,
        bool inLineScope)
    {
        if (logical.Op is LogicalOp.Not)
        {
            var operandPath = CompilePaths.LogicalOperand(path, LogicalOp.Not, 0);
            var operand = CompileExpr(logical.Operands[0], operandPath, registry, inLineScope);
            var operatorPath = $"{path}.not";

            return new CompiledPart(
                async (scope, cancellationToken) =>
                {
                    var value = await operand.Compiled(scope, cancellationToken).ConfigureAwait(false);
                    var result = Value.Of(!value.GetBool());
                    RecordNode(scope, operatorPath, "not", result, result.GetBool());
                    return result;
                },
                operand.MaxCost);
        }

        var parts = logical.Operands
            .Select((operand, index) => CompileExpr(
                operand,
                CompilePaths.LogicalOperand(path, logical.Op, index),
                registry,
                inLineScope))
            .OrderBy(part => part.MaxCost)
            .ToArray();

        var maxCost = parts.Length == 0 ? FactCost.Pure : parts.Max(part => part.MaxCost);
        var logicalPath = CompilePaths.ToOperatorName(logical.Op);
        var logicalOperatorPath = $"{path}.{logicalPath}";

        if (logical.Op is LogicalOp.All)
        {
            return new CompiledPart(
                async (scope, cancellationToken) =>
                {
                    foreach (var part in parts)
                    {
                        var value = await part.Compiled(scope, cancellationToken).ConfigureAwait(false);
                        if (!value.GetBool())
                        {
                            var result = Value.Of(false);
                            RecordNode(scope, logicalOperatorPath, logicalPath, result, passed: false);
                            return result;
                        }
                    }

                    var success = Value.Of(true);
                    RecordNode(scope, logicalOperatorPath, logicalPath, success, passed: true);
                    return success;
                },
                maxCost);
        }

        return new CompiledPart(
            async (scope, cancellationToken) =>
            {
                foreach (var part in parts)
                {
                    var value = await part.Compiled(scope, cancellationToken).ConfigureAwait(false);
                    if (value.GetBool())
                    {
                        var result = Value.Of(true);
                        RecordNode(scope, logicalOperatorPath, logicalPath, result, passed: true);
                        return result;
                    }
                }

                var failure = Value.Of(false);
                RecordNode(scope, logicalOperatorPath, logicalPath, failure, passed: false);
                return failure;
            },
            maxCost);
    }

    private static CompiledPart CompileCompare(
        CompareExpr compare,
        string path,
        IFactRegistry registry,
        bool inLineScope)
    {
        var comparePath = CompilePaths.CompareRoot(path, compare.Op);
        var left = CompileExpr(
            compare.Left,
            CompilePaths.CompareOperand(path, compare.Op, 0),
            registry,
            inLineScope);
        var right = CompileExpr(
            compare.Right,
            CompilePaths.CompareOperand(path, compare.Op, 1),
            registry,
            inLineScope);
        var operatorName = CompilePaths.ToOperatorName(compare.Op);

        return new CompiledPart(
            async (scope, cancellationToken) =>
            {
                var leftValue = await left.Compiled(scope, cancellationToken).ConfigureAwait(false);
                var rightValue = await right.Compiled(scope, cancellationToken).ConfigureAwait(false);
                var passed = EvaluateCompare(compare.Op, leftValue, rightValue);
                var result = Value.Of(passed);

                if (!passed &&
                    leftValue.Kind is ValueKind.Number &&
                    rightValue.Kind is ValueKind.Number)
                {
                    scope.Trace.RecordNearMiss(new NearMissRecord(
                        comparePath,
                        compare.Op,
                        leftValue.Number,
                        rightValue.Number,
                        Math.Abs(rightValue.Number - leftValue.Number)));
                }

                RecordNode(scope, comparePath, operatorName, result, passed);
                return result;
            },
            MaxCost(left.MaxCost, right.MaxCost));
    }

    private static CompiledPart CompileMembership(
        MembershipExpr membership,
        string path,
        IFactRegistry registry,
        bool inLineScope)
    {
        var membershipPath = $"{path}.{CompilePaths.ToOperatorName(membership.Op)}";
        var subject = CompileExpr(
            membership.Subject,
            CompilePaths.MembershipSubject(path, membership.Op),
            registry,
            inLineScope);

        if (membership.Op is MembershipOp.In)
        {
            var members = membership.Set
                .Select((member, index) => CompileExpr(
                    member,
                    CompilePaths.MembershipSetMember(path, membership.Op, index),
                    registry,
                    inLineScope))
                .ToArray();

            var maxCost = MaxCost(
                subject.MaxCost,
                members.Length == 0 ? FactCost.Pure : members.Max(member => member.MaxCost));

            return new CompiledPart(
                async (scope, cancellationToken) =>
                {
                    var subjectValue = await subject.Compiled(scope, cancellationToken).ConfigureAwait(false);

                    foreach (var member in members)
                    {
                        var memberValue = await member.Compiled(scope, cancellationToken).ConfigureAwait(false);
                        if (subjectValue.Equals(memberValue))
                        {
                            var success = Value.Of(true);
                            RecordNode(scope, membershipPath, "in", success, passed: true);
                            return success;
                        }
                    }

                    var failure = Value.Of(false);
                    RecordNode(scope, membershipPath, "in", failure, passed: false);
                    return failure;
                },
                maxCost);
        }

        var lower = CompileExpr(
            membership.Set[0],
            CompilePaths.MembershipBetweenBound(path, 0),
            registry,
            inLineScope);
        var upper = CompileExpr(
            membership.Set[1],
            CompilePaths.MembershipBetweenBound(path, 1),
            registry,
            inLineScope);

        return new CompiledPart(
            async (scope, cancellationToken) =>
            {
                var subjectValue = await subject.Compiled(scope, cancellationToken).ConfigureAwait(false);
                var lowerValue = await lower.Compiled(scope, cancellationToken).ConfigureAwait(false);
                var upperValue = await upper.Compiled(scope, cancellationToken).ConfigureAwait(false);

                var subjectNumber = subjectValue.GetNumber();
                var lowerNumber = lowerValue.GetNumber();
                var upperNumber = upperValue.GetNumber();
                var passed = subjectNumber >= lowerNumber && subjectNumber <= upperNumber;
                var result = Value.Of(passed);

                if (!passed)
                {
                    var shortfall = subjectNumber < lowerNumber
                        ? lowerNumber - subjectNumber
                        : subjectNumber - upperNumber;

                    scope.Trace.RecordNearMiss(new NearMissRecord(
                        membershipPath,
                        CompareOp.Gte,
                        subjectNumber,
                        subjectNumber < lowerNumber ? lowerNumber : upperNumber,
                        shortfall));
                }

                RecordNode(scope, membershipPath, "between", result, passed);
                return result;
            },
            MaxCost(subject.MaxCost, lower.MaxCost, upper.MaxCost));
    }

    private static CompiledPart CompileQuantifier(
        QuantifierExpr quantifier,
        string path,
        IFactRegistry registry)
    {
        var quantifierPath = CompilePaths.QuantifierPath(path, quantifier.Op);
        var where = CompileExpr(
            quantifier.Where,
            CompilePaths.QuantifierWherePath(path, quantifier.Op),
            registry,
            inLineScope: true);
        var operatorName = CompilePaths.ToOperatorName(quantifier.Op);

        return new CompiledPart(
            async (scope, cancellationToken) =>
            {
                var matchedAny = false;
                var matchedAll = true;

                foreach (var line in scope.Cart.Lines)
                {
                    var lineScope = scope.WithCurrentLine(line);
                    var matches = (await where.Compiled(lineScope, cancellationToken).ConfigureAwait(false)).GetBool();
                    matchedAny |= matches;
                    matchedAll &= matches;
                }

                var passed = quantifier.Op is QuantifierOp.Every ? matchedAll : matchedAny;
                var result = Value.Of(passed);
                RecordNode(scope, quantifierPath, operatorName, result, passed);
                return result;
            },
            where.MaxCost);
    }

    private static CompiledPart CompileAggregate(
        AggregateExpr aggregate,
        string path,
        IFactRegistry registry)
    {
        var aggregatePath = CompilePaths.AggregatePath(path, aggregate.Op);
        var where = CompileExpr(
            aggregate.Over.Where,
            CompilePaths.AggregateWherePath(path, aggregate.Op),
            registry,
            inLineScope: true);
        var operatorName = CompilePaths.ToOperatorName(aggregate.Op);

        return new CompiledPart(
            async (scope, cancellationToken) =>
            {
                var totals = new List<decimal>();

                foreach (var line in scope.Cart.Lines)
                {
                    var lineScope = scope.WithCurrentLine(line);
                    if ((await where.Compiled(lineScope, cancellationToken).ConfigureAwait(false)).GetBool())
                    {
                        totals.Add(Money.LineTotal(line.UnitPrice, line.Quantity));
                    }
                }

                var resultValue = aggregate.Op switch
                {
                    AggregateOp.Count => Value.Of(totals.Count),
                    AggregateOp.Sum => Value.Of(totals.Sum()),
                    AggregateOp.Min => Value.Of(totals.Count == 0 ? 0m : totals.Min()),
                    AggregateOp.Max => Value.Of(totals.Count == 0 ? 0m : totals.Max()),
                    _ => throw new ArgumentOutOfRangeException(nameof(aggregate)),
                };

                RecordNode(scope, aggregatePath, operatorName, resultValue, passed: null);
                return resultValue;
            },
            where.MaxCost);
    }

    private static CompiledPart CompileArithmetic(
        ArithmeticExpr arithmetic,
        string path,
        IFactRegistry registry,
        bool inLineScope)
    {
        var operands = arithmetic.Operands
            .Select((operand, index) => CompileExpr(
                operand,
                CompilePaths.ArithmeticOperand(path, arithmetic.Op, index),
                registry,
                inLineScope))
            .ToArray();

        var maxCost = operands.Length == 0 ? FactCost.Pure : operands.Max(operand => operand.MaxCost);
        var arithmeticPath = $"{path}.{CompilePaths.ToOperatorName(arithmetic.Op)}";
        var operatorName = CompilePaths.ToOperatorName(arithmetic.Op);

        return new CompiledPart(
            async (scope, cancellationToken) =>
            {
                var values = new decimal[operands.Length];
                for (var index = 0; index < operands.Length; index++)
                {
                    values[index] = (await operands[index].Compiled(scope, cancellationToken).ConfigureAwait(false))
                        .GetNumber();
                }

                var resultNumber = arithmetic.Op switch
                {
                    ArithmeticOp.Add => values.Sum(),
                    ArithmeticOp.Sub => values.Aggregate((current, next) => current - next),
                    ArithmeticOp.Mul => values.Aggregate(1m, (current, next) => current * next),
                    ArithmeticOp.MinOf => values.Min(),
                    ArithmeticOp.MaxOf => values.Max(),
                    _ => throw new ArgumentOutOfRangeException(nameof(arithmetic)),
                };

                var result = Value.Of(resultNumber);
                RecordNode(scope, arithmeticPath, operatorName, result, passed: null);
                return result;
            },
            maxCost);
    }

    private static bool EvaluateCompare(CompareOp op, Value left, Value right) =>
        op switch
        {
            CompareOp.Eq => left.Equals(right),
            CompareOp.Neq => !left.Equals(right),
            CompareOp.Gt => left.GetNumber() > right.GetNumber(),
            CompareOp.Gte => left.GetNumber() >= right.GetNumber(),
            CompareOp.Lt => left.GetNumber() < right.GetNumber(),
            CompareOp.Lte => left.GetNumber() <= right.GetNumber(),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };

    private static void RecordNode(EvalScope scope, string path, string operatorName, Value result, bool? passed)
    {
        if (scope.CaptureFullTrace)
        {
            scope.Trace.RecordNode(path, operatorName, result, passed);
        }
    }

    private static FactCost MaxCost(FactCost left, FactCost right) =>
        (FactCost)Math.Max((int)left, (int)right);

    private static FactCost MaxCost(FactCost first, FactCost second, FactCost third) =>
        (FactCost)Math.Max(Math.Max((int)first, (int)second), (int)third);

    private sealed record CompiledPart(Compiled Compiled, FactCost MaxCost);
}
