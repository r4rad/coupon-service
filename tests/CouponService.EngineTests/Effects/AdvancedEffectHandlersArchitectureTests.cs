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
            // Digests of LF-normalized file bytes (platform-independent).
            ["Compilation/Compiled.cs"] = "AEB85C15D81EC6DBBE589AB52DCB9716B310D07E9DD9075A13FF67AE441CD426",
            ["Compilation/CompiledCondition.cs"] = "5065E46635FABBDED2B34ECCFF4FD39A2113B365913BFE76A17716BBA4F7B4DD",
            ["Compilation/CompilePaths.cs"] = "95667C03CD22A71B35B963C9A73946FB56E917F6014B1DC096AA82123A154775",
            ["Compilation/PolicyCompiler.cs"] = "8E591C56E0BF5F58E693D44976556269A9FD0A67EC1B5D28F7F37E1274A9D397",
            ["Parsing/ParseBudget.cs"] = "BAB7B7D9E02112D326A865222B8F306D85E37F9DF945ABA8D8370AA87D9A5BDD",
            ["Parsing/PolicyBudgetException.cs"] = "F15078D240D71BE8036F97227AD26BDF992D740A792B407EC5AEAA0E3FDD8DAF",
            ["Parsing/PolicyParser.cs"] = "2E45E4F1358C90146534E887CC19487C70B9CB6EC74FE91BD2AF5EDF9FB3FFF4",
            ["Parsing/PolicySyntaxException.cs"] = "093FFBC7FCB5DC05FCCC85F90D8F3685D04BC12E716FF5A60D21A80AE3DBE998",
        };

    [Fact]
    public void Advanced_effect_handlers_register_without_touching_parser_or_compiler()
    {
        var engineRoot = Path.Combine(RepositoryRoot.Find(), "src", "CouponService.Engine");

        foreach (var (relativePath, expectedHash) in UnchangedEngineCoreFileHashes)
        {
            var absolutePath = Path.Combine(engineRoot, relativePath);
            Assert.True(File.Exists(absolutePath), $"Expected engine core file '{relativePath}' to exist.");

            // Hash LF-normalized bytes so Windows (CRLF) and Linux agents (LF) agree.
            // The expected digests were recorded from logical content, not from a specific checkout EOL.
            var actualHash = ComputeSha256Hex(NormalizeToLf(File.ReadAllBytes(absolutePath)));
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

    private static byte[] NormalizeToLf(byte[] content)
    {
        var text = Encoding.UTF8.GetString(content).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(text);
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
