using System.Reflection;
using System.Text.Json;
using CouponService.Application.Policies;
using CouponService.Application.Redemption;
using CouponService.Engine.Ast;
using CouponService.Engine.Facts;
using CouponService.Engine.Manifest;
using CouponService.Engine.Parsing;
using CouponService.Engine.Validation;

namespace CouponService.Api.Seeding;

public sealed record PolicySeedReport(int Created, int Updated, int Unchanged, int Total);

/// <summary>
/// Upserts the deterministic demo policy set (AC-9.5) so re-running converges without manual
/// cleanup (AC-9.6). Documents go through the same parse and validate path as the admin API, so
/// an invalid seed document fails loudly instead of reaching storage.
/// </summary>
public sealed class PolicySeeder(
    IPolicyRepository policies,
    IFactRegistry factRegistry,
    ILogger<PolicySeeder> logger)
{
    private const string ResourceName = "CouponService.Api.Seeding.SeedPolicies.json";

    private static readonly PolicyValidator Validator = new();

    /// <summary>
    /// The seed set as canonical compact JSON, in document order. Shared with
    /// scripts/seed-policies.ps1, which reads the same file.
    /// </summary>
    public static IReadOnlyList<string> ReadSeedDocuments()
    {
        using var stream = typeof(PolicySeeder).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded seed resource {ResourceName} is missing.");

        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind is not JsonValueKind.Array)
        {
            throw new InvalidOperationException($"{ResourceName} must contain a JSON array of policy documents.");
        }

        return document.RootElement
            .EnumerateArray()
            .Select(element => JsonSerializer.Serialize(element))
            .ToArray();
    }

    public async Task<PolicySeedReport> SeedAsync(CancellationToken cancellationToken)
    {
        var documents = ReadSeedDocuments();
        var created = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var documentJson in documents)
        {
            var record = Validate(documentJson);
            var existing = await policies
                .GetByPolicyIdAsync(record.PolicyId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                try
                {
                    await policies.CreateAsync(record, cancellationToken).ConfigureAwait(false);
                    created++;
                }
                catch (InvalidOperationException)
                {
                    // Another replica created the same partition key between the read and the write.
                    unchanged++;
                }

                continue;
            }

            if (string.Equals(existing.DocumentJson, record.DocumentJson, StringComparison.Ordinal))
            {
                unchanged++;
                continue;
            }

            try
            {
                await policies
                    .ReplaceAsync(record, existing.ETag, cancellationToken)
                    .ConfigureAwait(false);
                updated++;
            }
            catch (PreconditionFailedException)
            {
                // Lost the race to another replica seeding the identical document.
                unchanged++;
            }
        }

        var report = new PolicySeedReport(created, updated, unchanged, documents.Count);
        logger.LogInformation(
            "Policy seed complete: {Created} created, {Updated} updated, {Unchanged} unchanged, {Total} total.",
            report.Created,
            report.Updated,
            report.Unchanged,
            report.Total);

        return report;
    }

    private PolicyRecord Validate(string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("condition", out var condition))
        {
            throw new InvalidOperationException("Seed policy document requires $.condition.");
        }

        var engineSchema = root.TryGetProperty("engineSchema", out var schemaElement)
            ? schemaElement.GetString() ?? string.Empty
            : string.Empty;

        var budget = new ParseBudget(
            EngineLimits.Default.MaxParseNodes,
            EngineLimits.Default.MaxParseDepth);
        Expr conditionExpr = PolicyParser.Parse(condition.Clone(), budget, PolicyValidator.ConditionPath);

        var validation = Validator.Validate(engineSchema, conditionExpr, factRegistry);
        if (!validation.IsValid)
        {
            var detail = string.Join("; ", validation.Errors.Select(error => $"{error.Path}: {error.Message}"));
            throw new InvalidOperationException($"Seed policy document is invalid. {detail}");
        }

        return PolicyRecordFactory.FromDocument(documentJson);
    }
}
