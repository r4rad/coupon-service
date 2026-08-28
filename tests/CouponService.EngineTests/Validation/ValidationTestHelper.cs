using System.Text.Json;
using CouponService.Engine.Ast;
using CouponService.Engine.Facts;
using CouponService.Engine.Manifest;
using CouponService.Engine.Parsing;
using CouponService.Engine.Validation;

namespace CouponService.EngineTests.Validation;

internal static class ValidationTestHelper
{
    internal static IFactRegistry Registry => StandardFactVocabulary.Create();

    internal static PolicyValidator Validator { get; } = new();

    internal static Expr ParseCondition(string json, ParseBudget? budget = null)
    {
        using var document = JsonDocument.Parse(json);
        return PolicyParser.Parse(
            document.RootElement,
            budget ?? new ParseBudget(EngineLimits.Default.MaxParseNodes, EngineLimits.Default.MaxParseDepth),
            PolicyValidator.ConditionPath);
    }

    internal static PolicyValidationResult ValidateCondition(string engineSchema, string conditionJson) =>
        Validator.Validate(
            engineSchema,
            ParseCondition(conditionJson),
            Registry,
            PolicyValidator.ConditionPath);
}
