using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CouponService.Domain;
using CouponService.Engine.Effects;
using CouponService.Engine.Manifest;

namespace CouponService.EngineTests.Effects;

public sealed class AdvancedEffectHandlersArchitectureTests
{
    private static readonly IReadOnlyDictionary<string, string> UnchangedEngineCoreFileHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Compilation/Compiled.cs"] = "1F20BC747D411055624A44A920EB32BD91AA8AC629C2346C30820B474DD7FB39",
            ["Compilation/CompiledCondition.cs"] = "27762BFB51D2FBE24D563599FB5DC4502AA81B0D3CFA7D378021218EE5EAC233",
            ["Compilation/CompilePaths.cs"] = "A58E6DDD81E4641569DA9E8EAD5CCC828AD9D5F9AAD100BC107657DA235CBEBD",
            ["Compilation/PolicyCompiler.cs"] = "99B86019F7587C6F6084CC8017E16A22071E19EBA328E500B693BCF34FC5371B",
            ["Parsing/ParseBudget.cs"] = "EA2E9FE5AB7556BD42B4D805F1DF7C6055F2F4B22FDD9BBB3A4283285ECF82FB",
            ["Parsing/PolicyBudgetException.cs"] = "B7CB25A5BCD41358C0BFE04367B4CECB68231526E6A02FFB435AD7D5FFACA280",
            ["Parsing/PolicyParser.cs"] = "325212F4324B960451134CBA7314FAD2CE43EDCB8A3DB1EDCF56C3E3850B233A",
            ["Parsing/PolicySyntaxException.cs"] = "2481645E5E64F6024D8CDAD486751BEDD0EC6A3B00B0514D7A0B8D325432FAFF",
        };

    [Fact]
    public void Advanced_effect_handlers_register_without_touching_parser_or_compiler()
    {
        var engineRoot = Path.Combine(RepositoryRoot.Find(), "src", "CouponService.Engine");

        foreach (var (relativePath, expectedHash) in UnchangedEngineCoreFileHashes)
        {
            var absolutePath = Path.Combine(engineRoot, relativePath);
            Assert.True(File.Exists(absolutePath), $"Expected engine core file '{relativePath}' to exist.");

            var actualHash = ComputeSha256Hex(File.ReadAllBytes(absolutePath));
            Assert.Equal(expectedHash, actualHash);
        }
    }

    [Fact]
    public void New_rule_using_registered_facts_and_advanced_effects_applies_without_engine_core_changes()
    {
        var cart = EffectsTestHelper.CreateVegetarianCart(29.00m);
        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
              "cheapestFree": {
                "count": 1,
                "from": {
                  "lines": {
                    "where": { "eq": [ { "fact": "line.category" }, "Vegetarian" ] }
                  }
                }
              }
            }
            """,
            cart);

        Assert.True(plan.Total > 0);
        Assert.Equal(plan.Allocations.Sum(allocation => allocation.Amount), plan.Total);
    }

    [Fact]
    public void Standard_handler_collection_exposes_every_catalogued_effect_operator()
    {
        var registeredOperators = EffectEngine.CreateStandardHandlers()
            .Select(handler => handler.Operator)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var effectOperator in EngineCatalog.EffectOperators)
        {
            Assert.Contains(effectOperator, registeredOperators);
        }
    }

    private static string ComputeSha256Hex(ReadOnlySpan<byte> content)
    {
        var hashBytes = SHA256.HashData(content);
        var builder = new StringBuilder(hashBytes.Length * 2);

        foreach (var hashByte in hashBytes)
        {
            builder.Append(hashByte.ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
