using System.Collections.Immutable;
using System.Text.Json;
using CouponService.Engine.Ast;

namespace CouponService.Engine.Parsing;

public static class PolicyParser
{
    public static Expr Parse(JsonElement element, ParseBudget budget, string path = "$")
    {
        budget.Spend();

        return element.ValueKind switch
        {
            JsonValueKind.Number => new ConstExpr(Value.Of(element.GetDecimal())),
            JsonValueKind.String => new ConstExpr(Value.Of(element.GetString()!)),
            JsonValueKind.True => new ConstExpr(Value.Of(true)),
            JsonValueKind.False => new ConstExpr(Value.Of(false)),
            JsonValueKind.Array => ParseConstantArray(element, path),
            JsonValueKind.Object => ParseObject(element, budget, path),
            _ => throw new PolicySyntaxException(path, $"Unsupported JSON token '{element.ValueKind}'."),
        };
    }

    private static Expr ParseObject(JsonElement element, ParseBudget budget, string path)
    {
        var property = RequireSingleProperty(element, path);

        return property.Name switch
        {
            "fact" => ParseFact(property.Value, path),
            "all" or "any" or "not" => ParseLogical(property.Name, property.Value, budget, path),
            "eq" or "neq" or "gt" or "gte" or "lt" or "lte" => ParseCompare(property.Name, property.Value, budget, path),
            "in" or "between" => ParseMembership(property.Name, property.Value, budget, path),
            "every" or "some" => ParseQuantifier(property.Name, property.Value, budget, path),
            "sum" or "count" or "min" or "max" => ParseAggregate(property.Name, property.Value, budget, path),
            "add" or "sub" or "mul" or "minOf" or "maxOf" => ParseArithmetic(property.Name, property.Value, budget, path),
            _ => throw new PolicySyntaxException(path, $"Unknown operator '{property.Name}'."),
        };
    }

    private static Expr ParseFact(JsonElement value, string path)
    {
        if (value.ValueKind is not JsonValueKind.String)
        {
            throw new PolicySyntaxException(AppendPath(path, "fact"), "Fact path must be a string.");
        }

        return new FactExpr(value.GetString()!);
    }

    private static Expr ParseLogical(string operatorName, JsonElement value, ParseBudget budget, string path)
    {
        var op = operatorName switch
        {
            "all" => LogicalOp.All,
            "any" => LogicalOp.Any,
            "not" => LogicalOp.Not,
            _ => throw new PolicySyntaxException(path, $"Unknown logical operator '{operatorName}'."),
        };

        if (op is LogicalOp.Not)
        {
            var operandPath = AppendPath(path, operatorName);
            var operand = Parse(value, budget.Deeper(), operandPath);
            return new LogicalExpr(op, ImmutableArray.Create(operand));
        }

        var operands = ParseExpressionArray(value, budget, AppendPath(path, operatorName));
        if (operands.IsEmpty)
        {
            throw new PolicySyntaxException(AppendPath(path, operatorName), "Logical operator requires at least one operand.");
        }

        return new LogicalExpr(op, operands);
    }

    private static Expr ParseCompare(string operatorName, JsonElement value, ParseBudget budget, string path)
    {
        var operands = ParseExpressionArray(value, budget, AppendPath(path, operatorName));
        if (operands.Length != 2)
        {
            throw new PolicySyntaxException(
                AppendPath(path, operatorName),
                "Comparison operator requires exactly two operands.");
        }

        var op = operatorName switch
        {
            "eq" => CompareOp.Eq,
            "neq" => CompareOp.Neq,
            "gt" => CompareOp.Gt,
            "gte" => CompareOp.Gte,
            "lt" => CompareOp.Lt,
            "lte" => CompareOp.Lte,
            _ => throw new PolicySyntaxException(path, $"Unknown comparison operator '{operatorName}'."),
        };

        return new CompareExpr(op, operands[0], operands[1]);
    }

    private static Expr ParseMembership(string operatorName, JsonElement value, ParseBudget budget, string path)
    {
        var operands = ParseExpressionArray(value, budget, AppendPath(path, operatorName));

        return operatorName switch
        {
            "in" => ParseInMembership(operands, AppendPath(path, operatorName)),
            "between" => ParseBetweenMembership(operands, AppendPath(path, operatorName)),
            _ => throw new PolicySyntaxException(path, $"Unknown membership operator '{operatorName}'."),
        };
    }

    private static Expr ParseInMembership(ImmutableArray<Expr> operands, string path)
    {
        if (operands.Length != 2)
        {
            throw new PolicySyntaxException(path, "'in' requires exactly two operands.");
        }

        if (operands[1] is not ConstExpr { Value.Kind: ValueKind.List } listConstant)
        {
            throw new PolicySyntaxException(
                AppendPath(path, "[1]"),
                "'in' set must be a constant list.");
        }

        var set = listConstant.Value.GetList()
            .Select(static item => (Expr)new ConstExpr(item))
            .ToImmutableArray();

        return new MembershipExpr(MembershipOp.In, operands[0], set);
    }

    private static Expr ParseBetweenMembership(ImmutableArray<Expr> operands, string path)
    {
        if (operands.Length != 3)
        {
            throw new PolicySyntaxException(path, "'between' requires exactly three operands.");
        }

        return new MembershipExpr(
            MembershipOp.Between,
            operands[0],
            ImmutableArray.Create(operands[1], operands[2]));
    }

    private static Expr ParseQuantifier(string operatorName, JsonElement value, ParseBudget budget, string path)
    {
        if (value.ValueKind is not JsonValueKind.Object)
        {
            throw new PolicySyntaxException(AppendPath(path, operatorName), "Quantifier value must be an object.");
        }

        var overPath = AppendPath(path, operatorName);
        string? over = null;
        Expr? where = null;

        foreach (var property in value.EnumerateObject())
        {
            switch (property.Name)
            {
                case "over":
                    if (property.Value.ValueKind is not JsonValueKind.String)
                    {
                        throw new PolicySyntaxException(
                            AppendPath(overPath, "over"),
                            "Quantifier 'over' must be a string.");
                    }

                    over = property.Value.GetString();
                    break;
                case "where":
                    where = Parse(property.Value, budget.Deeper(), AppendPath(overPath, "where"));
                    break;
                default:
                    throw new PolicySyntaxException(
                        AppendPath(overPath, property.Name),
                        $"Unknown quantifier property '{property.Name}'.");
            }
        }

        if (over is null)
        {
            throw new PolicySyntaxException(overPath, "Quantifier requires an 'over' property.");
        }

        if (where is null)
        {
            throw new PolicySyntaxException(overPath, "Quantifier requires a 'where' property.");
        }

        var op = operatorName switch
        {
            "every" => QuantifierOp.Every,
            "some" => QuantifierOp.Some,
            _ => throw new PolicySyntaxException(path, $"Unknown quantifier operator '{operatorName}'."),
        };

        return new QuantifierExpr(op, over, where);
    }

    private static Expr ParseAggregate(string operatorName, JsonElement value, ParseBudget budget, string path)
    {
        var op = operatorName switch
        {
            "sum" => AggregateOp.Sum,
            "count" => AggregateOp.Count,
            "min" => AggregateOp.Min,
            "max" => AggregateOp.Max,
            _ => throw new PolicySyntaxException(path, $"Unknown aggregate operator '{operatorName}'."),
        };

        return new AggregateExpr(op, ParseSelector(value, budget, AppendPath(path, operatorName)));
    }

    private static Expr ParseArithmetic(string operatorName, JsonElement value, ParseBudget budget, string path)
    {
        var operands = ParseExpressionArray(value, budget, AppendPath(path, operatorName));
        if (operands.Length < 2)
        {
            throw new PolicySyntaxException(
                AppendPath(path, operatorName),
                "Arithmetic operator requires at least two operands.");
        }

        var op = operatorName switch
        {
            "add" => ArithmeticOp.Add,
            "sub" => ArithmeticOp.Sub,
            "mul" => ArithmeticOp.Mul,
            "minOf" => ArithmeticOp.MinOf,
            "maxOf" => ArithmeticOp.MaxOf,
            _ => throw new PolicySyntaxException(path, $"Unknown arithmetic operator '{operatorName}'."),
        };

        return new ArithmeticExpr(op, operands);
    }

    public static Selector ParseSelector(JsonElement element, ParseBudget budget, string path)
    {
        var property = RequireSingleProperty(element, path);
        if (property.Name is not "lines")
        {
            throw new PolicySyntaxException(path, "Selector must use the 'lines' property.");
        }

        var linesPath = AppendPath(path, "lines");
        var whereProperty = RequireSingleProperty(property.Value, linesPath);
        if (whereProperty.Name is not "where")
        {
            throw new PolicySyntaxException(linesPath, "Selector requires a 'where' property.");
        }

        var where = Parse(whereProperty.Value, budget.Deeper(), AppendPath(linesPath, "where"));
        return new Selector(where);
    }

    private static ConstExpr ParseConstantArray(JsonElement element, string path)
    {
        var values = element.EnumerateArray()
            .Select((item, index) => ReadScalar(item, AppendPath(path, $"[{index}]")))
            .ToImmutableArray();

        return new ConstExpr(Value.Of(values));
    }

    private static ImmutableArray<Expr> ParseExpressionArray(
        JsonElement element,
        ParseBudget budget,
        string path)
    {
        if (element.ValueKind is not JsonValueKind.Array)
        {
            throw new PolicySyntaxException(path, "Operator value must be an array.");
        }

        var expressions = ImmutableArray.CreateBuilder<Expr>();
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            expressions.Add(Parse(item, budget.Deeper(), AppendPath(path, $"[{index}]")));
            index++;
        }

        return expressions.ToImmutable();
    }

    private static Value ReadScalar(JsonElement element, string path) =>
        element.ValueKind switch
        {
            JsonValueKind.Number => Value.Of(element.GetDecimal()),
            JsonValueKind.String => Value.Of(element.GetString()!),
            JsonValueKind.True => Value.Of(true),
            JsonValueKind.False => Value.Of(false),
            _ => throw new PolicySyntaxException(path, $"Unsupported scalar token '{element.ValueKind}'."),
        };

    private static JsonProperty RequireSingleProperty(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            throw new PolicySyntaxException(path, "Operator node must be a JSON object.");
        }

        using var enumerator = element.EnumerateObject();
        if (!enumerator.MoveNext())
        {
            throw new PolicySyntaxException(path, "Operator node must contain exactly one property.");
        }

        var property = enumerator.Current;
        if (enumerator.MoveNext())
        {
            throw new PolicySyntaxException(path, "Operator node must contain exactly one property.");
        }

        return property;
    }

    private static string AppendPath(string path, string segment) =>
        segment.StartsWith("[", StringComparison.Ordinal) ? path + segment : $"{path}.{segment}";
}
