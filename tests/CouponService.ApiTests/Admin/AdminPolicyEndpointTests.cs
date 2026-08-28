using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CouponService.Api.Controllers.V1;
using CouponService.Application.Policies;
using CouponService.Engine.Facts;
using CouponService.Engine.Manifest;

namespace CouponService.ApiTests.Admin;

public sealed class AdminPolicyEndpointTests : IClassFixture<AdminApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AdminApiFactory _factory;

    public AdminPolicyEndpointTests(AdminApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_validates_against_manifest_and_persists_policy()
    {
        using var client = _factory.CreateClient();
        var document = ParseDocument(AdminTestDocuments.ValidDraft("create-ok", "CREATEOK"));

        var response = await client.PostAsJsonAsync("/v1/admin/policies", document);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ReadPolicyAsync(response);
        Assert.Equal("create-ok", body.PolicyId);
        Assert.Equal("CREATEOK", body.Code);
        Assert.Equal(PolicyStatus.Draft, body.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.ETag));

        var stored = await _factory.Policies.GetByPolicyIdAsync("create-ok");
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task Create_with_unknown_fact_returns_400_listing_offending_node()
    {
        var writesBefore = _factory.Policies.WriteCount;
        using var client = _factory.CreateClient();
        var document = ParseDocument(AdminTestDocuments.UnknownFact("unknown-fact", "UNKNOWNFACT"));

        var response = await client.PostAsJsonAsync("/v1/admin/policies", document);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(writesBefore, _factory.Policies.WriteCount);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.NotEmpty(errors.EnumerateArray());

        var first = errors.EnumerateArray().First();
        Assert.Equal("$.condition.fact", first.GetProperty("path").GetString());
        Assert.Contains("customer.zodiacSign", first.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_with_stale_etag_returns_412()
    {
        using var client = _factory.CreateClient();
        const string policyId = "etag-stale";
        const string code = "ETAGSTALE";

        var created = await CreatePolicyAsync(client, AdminTestDocuments.ValidDraft(policyId, code));
        var staleEtag = created.ETag;

        var firstUpdate = AdminTestDocuments.UpdatedDraft(policyId, code);
        using var firstRequest = CreatePutRequest(policyId, firstUpdate, created.ETag);
        var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var staleRequest = CreatePutRequest(
            policyId,
            AdminTestDocuments.UpdatedDraft(policyId, code),
            staleEtag);
        var staleResponse = await client.SendAsync(staleRequest);

        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_archives_policy_and_leaves_document_readable()
    {
        using var client = _factory.CreateClient();
        const string policyId = "archive-me";
        const string code = "ARCHIVEME";

        _ = await CreatePolicyAsync(client, AdminTestDocuments.ValidDraft(policyId, code));

        var deleteResponse = await client.DeleteAsync($"/v1/admin/policies/{policyId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var deleted = await ReadPolicyAsync(deleteResponse);
        Assert.Equal(PolicyStatus.Archived, deleted.Status);

        var getResponse = await client.GetAsync($"/v1/admin/policies/{policyId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var readable = await ReadPolicyAsync(getResponse);
        Assert.Equal(PolicyStatus.Archived, readable.Status);
        Assert.Equal(policyId, readable.PolicyId);

        var stored = await _factory.Policies.GetByPolicyIdAsync(policyId);
        Assert.NotNull(stored);
        Assert.Equal(PolicyStatus.Archived, PolicyDocumentMetadata.ReadStatus(stored!.DocumentJson));
    }

    [Fact]
    public async Task List_returns_created_policies()
    {
        using var client = _factory.CreateClient();
        _ = await CreatePolicyAsync(client, AdminTestDocuments.ValidDraft("list-a", "LISTA"));
        _ = await CreatePolicyAsync(client, AdminTestDocuments.ValidDraft("list-b", "LISTB"));

        var response = await client.GetAsync("/v1/admin/policies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AdminPolicyResponse[]>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains(body!, policy => policy.PolicyId == "list-a");
        Assert.Contains(body!, policy => policy.PolicyId == "list-b");
    }

    private async Task<AdminPolicyResponse> CreatePolicyAsync(HttpClient client, string documentJson)
    {
        var response = await client.PostAsJsonAsync("/v1/admin/policies", ParseDocument(documentJson));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadPolicyAsync(response);
    }

    private static HttpRequestMessage CreatePutRequest(string policyId, string documentJson, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/v1/admin/policies/{policyId}")
        {
            Content = new StringContent(documentJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return request;
    }

    private static JsonElement ParseDocument(string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        return document.RootElement.Clone();
    }

    private static async Task<AdminPolicyResponse> ReadPolicyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<AdminPolicyResponse>(JsonOptions);
        return body ?? throw new InvalidOperationException("Policy response body was empty.");
    }
}

public sealed class ManifestEndpointTests : IClassFixture<AdminApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AdminApiFactory _factory;

    public ManifestEndpointTests(AdminApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Manifest_returns_every_registered_fact_operator_effect_and_limit()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/policy-engine/manifest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var manifest = await response.Content.ReadFromJsonAsync<EngineManifest>(JsonOptions);
        Assert.NotNull(manifest);

        var registry = StandardFactVocabulary.Create();
        Assert.Equal(EngineManifestGenerator.CurrentEngineSchema, manifest!.EngineSchema);
        Assert.Equal(registry.All.Count, manifest.Facts.Count);

        foreach (var descriptor in registry.All)
        {
            Assert.Contains(
                manifest.Facts,
                fact => fact.Path == descriptor.Path
                    && fact.Kind == descriptor.Kind
                    && fact.Cost == descriptor.Cost);
        }

        Assert.Equal(EngineCatalog.ConditionOperators, manifest.ConditionOperators);
        Assert.Equal(EngineCatalog.EffectOperators, manifest.EffectOperators);
        Assert.Equal(EngineLimits.Default, manifest.Limits);
    }
}
