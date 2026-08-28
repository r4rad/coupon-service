using System.Collections.Immutable;
using CouponService.Domain;
using CouponService.Engine.Ast;
using CouponService.Engine.Compilation;
using CouponService.Engine.Evaluation;
using CouponService.Engine.Parsing;
using System.Text.Json;

namespace CouponService.Engine.Effects;

public sealed class SelectorEvaluator
{
    private readonly PolicyCompiler _compiler = new();

    public ImmutableArray<CartLine> SelectLines(
        Selector selector,
        EvalScope evalScope,
        CancellationToken cancellationToken)
    {
        var compiled = _compiler.Compile(selector.Where, evalScope.Registry);
        var selected = ImmutableArray.CreateBuilder<CartLine>();

        foreach (var line in evalScope.Cart.Lines)
        {
            var lineScope = evalScope.WithCurrentLine(line);
            var matches = compiled.Condition(lineScope, cancellationToken).GetAwaiter().GetResult().GetBool();
            if (matches)
            {
                selected.Add(line);
            }
        }

        return selected.ToImmutable();
    }

    public ImmutableArray<CartLine> SelectLines(
        JsonElement selectorElement,
        string path,
        ParseBudget budget,
        EvalScope evalScope,
        CancellationToken cancellationToken)
    {
        var selector = PolicyParser.ParseSelector(selectorElement, budget, path);
        return SelectLines(selector, evalScope, cancellationToken);
    }
}
