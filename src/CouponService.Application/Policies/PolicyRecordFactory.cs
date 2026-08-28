using System.Text.Json;

namespace CouponService.Application.Policies;

public static class PolicyRecordFactory
{
    public static PolicyRecord FromDocument(string documentJson, string etag = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentJson);

        using var document = JsonDocument.Parse(documentJson);
        var root = document.RootElement;
        var policyId = root.GetProperty("policyId").GetString()
            ?? throw new InvalidOperationException("Policy document is missing $.policyId.");

        var trigger = PolicyDocumentMetadata.ReadTrigger(documentJson);
        string? code = null;
        if (root.TryGetProperty("code", out var codeElement)
            && codeElement.ValueKind is not JsonValueKind.Null)
        {
            code = codeElement.GetString();
        }

        var partitionKey = trigger is PolicyTrigger.Automatic
            ? PolicyPartitionKey.ForAutomatic(policyId)
            : PolicyPartitionKey.ForCode(code ?? throw new InvalidOperationException(
                "Coded policy document is missing $.code."));

        return new PolicyRecord(partitionKey, policyId, code, trigger, documentJson, etag);
    }
}
