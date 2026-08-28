using CouponService.Engine.Ast;
using CouponService.Engine.Facts;
using CouponService.Engine.Manifest;

namespace CouponService.Engine.Validation;

public sealed class PolicyValidator
{
    public const string ConditionPath = "$.condition";

    public PolicyValidationResult Validate(string engineSchema, Expr condition, IFactRegistry registry) =>
        Validate(engineSchema, condition, registry, ConditionPath);

    public PolicyValidationResult Validate(
        string engineSchema,
        Expr condition,
        IFactRegistry registry,
        string conditionPath)
    {
        var collector = new ValidationCollector(registry);

        if (!string.Equals(engineSchema, EngineManifestGenerator.CurrentEngineSchema, StringComparison.Ordinal))
        {
            collector.Add(
                "$.engineSchema",
                $"Engine schema '{engineSchema}' is not supported.");
        }

        ValidateExpression(condition, conditionPath, collector, inLineScope: false);

        return collector.ToResult();
    }

    private static void ValidateExpression(
        Expr expression,
        string path,
        ValidationCollector collector,
        bool inLineScope)
    {
        switch (expression)
        {
            case FactExpr fact:
                ValidateFact(fact, ValidationPaths.FactPath(path), collector, inLineScope);
                break;
            case CompareExpr compare:
                ValidateCompare(compare, path, collector, inLineScope);
                break;
            case MembershipExpr membership:
                ValidateMembership(membership, path, collector, inLineScope);
                break;
            case LogicalExpr logical:
                ValidateLogical(logical, path, collector, inLineScope);
                break;
            case QuantifierExpr quantifier:
                ValidateQuantifier(quantifier, path, collector);
                break;
            case AggregateExpr aggregate:
                ValidateAggregate(aggregate, path, collector, inLineScope);
                break;
            case ArithmeticExpr arithmetic:
                ValidateArithmetic(arithmetic, path, collector, inLineScope);
                break;
            case ConstExpr:
                break;
        }
    }

    private static void ValidateFact(
        FactExpr fact,
        string path,
        ValidationCollector collector,
        bool inLineScope)
    {
        if (!collector.Registry.TryGet(fact.Path, out _))
        {
            collector.Add(path, $"Unknown fact '{fact.Path}'.");
            return;
        }

        if (fact.Path.StartsWith("line.", StringComparison.Ordinal) && !inLineScope)
        {
            collector.Add(
                path,
                $"Fact '{fact.Path}' is only valid inside a quantifier over cart.lines.");
        }
    }

    private static void ValidateCompare(
        CompareExpr compare,
        string path,
        ValidationCollector collector,
        bool inLineScope)
    {
        var comparePath = ValidationPaths.CompareRoot(path, compare.Op);
        var leftPath = ValidationPaths.CompareOperand(path, compare.Op, 0);
        var rightPath = ValidationPaths.CompareOperand(path, compare.Op, 1);

        ValidateExpression(compare.Left, leftPath, collector, inLineScope);
        ValidateExpression(compare.Right, rightPath, collector, inLineScope);

        var leftType = InferType(compare.Left, collector, inLineScope);
        var rightType = InferType(compare.Right, collector, inLineScope);

        if (leftType is not null && rightType is not null && leftType != rightType)
        {
            collector.Add(
                comparePath,
                $"Comparison operands have incompatible types '{leftType}' and '{rightType}'.");
        }
    }

    private static void ValidateMembership(
        MembershipExpr membership,
        string path,
        ValidationCollector collector,
        bool inLineScope)
    {
        var subjectPath = ValidationPaths.MembershipSubject(path, membership.Op);
        ValidateExpression(membership.Subject, subjectPath, collector, inLineScope);

        var subjectType = InferType(membership.Subject, collector, inLineScope);

        if (membership.Op is MembershipOp.In)
        {
            for (var index = 0; index < membership.Set.Length; index++)
            {
                var memberPath = ValidationPaths.MembershipSetMember(path, membership.Op, index);
                ValidateExpression(membership.Set[index], memberPath, collector, inLineScope);

                var memberType = InferType(membership.Set[index], collector, inLineScope);
                if (subjectType is not null && memberType is not null && subjectType != memberType)
                {
                    collector.Add(
                        path,
                        $"'in' subject type '{subjectType}' is incompatible with set member type '{memberType}'.");
                }
            }

            return;
        }

        if (membership.Set.Length != 2)
        {
            collector.Add(path, "'between' requires exactly two bound operands.");
            return;
        }

        var lowerPath = ValidationPaths.MembershipBetweenBound(path, 0);
        var upperPath = ValidationPaths.MembershipBetweenBound(path, 1);
        ValidateExpression(membership.Set[0], lowerPath, collector, inLineScope);
        ValidateExpression(membership.Set[1], upperPath, collector, inLineScope);

        var lowerType = InferType(membership.Set[0], collector, inLineScope);
        var upperType = InferType(membership.Set[1], collector, inLineScope);

        if (subjectType is not null && subjectType != ValueKind.Number)
        {
            collector.Add(subjectPath, "'between' subject must be numeric.");
        }

        if (lowerType is not null && lowerType != ValueKind.Number)
        {
            collector.Add(lowerPath, "'between' lower bound must be numeric.");
        }

        if (upperType is not null && upperType != ValueKind.Number)
        {
            collector.Add(upperPath, "'between' upper bound must be numeric.");
        }
    }

    private static void ValidateLogical(
        LogicalExpr logical,
        string path,
        ValidationCollector collector,
        bool inLineScope)
    {
        for (var index = 0; index < logical.Operands.Length; index++)
        {
            var operandPath = ValidationPaths.LogicalOperand(path, logical.Op, index);
            ValidateExpression(logical.Operands[index], operandPath, collector, inLineScope);

            var operandType = InferType(logical.Operands[index], collector, inLineScope);
            if (operandType is not null && operandType != ValueKind.Bool)
            {
                collector.Add(operandPath, $"Logical operator requires boolean operands, found '{operandType}'.");
            }
        }
    }

    private static void ValidateQuantifier(QuantifierExpr quantifier, string path, ValidationCollector collector)
    {
        var quantifierPath = ValidationPaths.QuantifierPath(path, quantifier.Op);

        if (!string.Equals(quantifier.Over, "cart.lines", StringComparison.Ordinal))
        {
            collector.Add(
                $"{quantifierPath}.over",
                $"Quantifier 'over' must be 'cart.lines', found '{quantifier.Over}'.");
        }

        ValidateExpression(quantifier.Where, $"{quantifierPath}.where", collector, inLineScope: true);
    }

    private static void ValidateAggregate(
        AggregateExpr aggregate,
        string path,
        ValidationCollector collector,
        bool inLineScope)
    {
        var aggregatePath = ValidationPaths.AggregatePath(path, aggregate.Op);
        var wherePath = $"{aggregatePath}.lines.where";

        ValidateExpression(aggregate.Over.Where, wherePath, collector, inLineScope: true);

        var whereType = InferType(aggregate.Over.Where, collector, inLineScope: true);
        if (whereType is not null && whereType != ValueKind.Bool)
        {
            collector.Add(wherePath, "Aggregate selector 'where' must be boolean.");
        }
    }

    private static void ValidateArithmetic(
        ArithmeticExpr arithmetic,
        string path,
        ValidationCollector collector,
        bool inLineScope)
    {
        for (var index = 0; index < arithmetic.Operands.Length; index++)
        {
            var operandPath = ValidationPaths.ArithmeticOperand(path, arithmetic.Op, index);
            ValidateExpression(arithmetic.Operands[index], operandPath, collector, inLineScope);

            var operandType = InferType(arithmetic.Operands[index], collector, inLineScope);
            if (operandType is not null && operandType != ValueKind.Number)
            {
                collector.Add(operandPath, $"Arithmetic operator requires numeric operands, found '{operandType}'.");
            }
        }
    }

    private static ValueKind? InferType(Expr expression, ValidationCollector collector, bool inLineScope) =>
        expression switch
        {
            ConstExpr constant => constant.Value.Kind,
            FactExpr fact => collector.Registry.TryGet(fact.Path, out var descriptor) ? descriptor.Kind : null,
            CompareExpr => ValueKind.Bool,
            MembershipExpr membership => membership.Op is MembershipOp.In or MembershipOp.Between
                ? ValueKind.Bool
                : null,
            LogicalExpr => ValueKind.Bool,
            QuantifierExpr => ValueKind.Bool,
            AggregateExpr => ValueKind.Number,
            ArithmeticExpr => ValueKind.Number,
            _ => null,
        };

    private sealed class ValidationCollector(IFactRegistry registry)
    {
        private readonly List<PolicyValidationError> _errors = [];

        public IFactRegistry Registry { get; } = registry;

        public void Add(string path, string message) => _errors.Add(new PolicyValidationError(path, message));

        public PolicyValidationResult ToResult() => _errors.Count == 0
            ? PolicyValidationResult.Valid
            : new PolicyValidationResult(_errors);
    }
}
