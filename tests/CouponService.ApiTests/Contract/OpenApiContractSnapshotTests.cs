using System.Net;
using System.Reflection;

namespace CouponService.ApiTests.Contract;

public sealed class OpenApiContractSnapshotTests : IClassFixture<ContractApiFactory>
{
    private const string SnapshotResourceName =
        "CouponService.ApiTests.Contract.Snapshots.coupon-service-openapi.v1.json";

    private readonly ContractApiFactory _factory;

    public OpenApiContractSnapshotTests(ContractApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApi_document_matches_committed_snapshot()
    {
        using var client = _factory.CreateAnonymousClient();
        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = ContractJsonNormalizer.Normalize(await response.Content.ReadAsStringAsync());
        var expected = ContractJsonNormalizer.Normalize(await ReadSnapshotAsync());

        Assert.Equal(expected, actual);
    }

    private static async Task<string> ReadSnapshotAsync()
    {
        await using var stream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream(SnapshotResourceName);

        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded snapshot '{SnapshotResourceName}' was not found.");
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
