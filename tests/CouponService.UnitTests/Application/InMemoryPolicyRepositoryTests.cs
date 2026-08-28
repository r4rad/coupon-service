using CouponService.Application.Policies;
using CouponService.Infrastructure.InMemory;

namespace CouponService.UnitTests.Application;

public sealed class InMemoryPolicyRepositoryTests
{
    [Fact]
    public async Task Replace_with_stale_etag_throws_precondition_failed()
    {
        var repository = new InMemoryPolicyRepository();
        var created = await repository.CreateAsync(new PolicyRecord(
            "SAVE10",
            "save10",
            "SAVE10",
            PolicyTrigger.Code,
            """{"engineSchema":"1.0","status":"Active","condition":{"gte":[{"fact":"cart.subtotal"},0]},"effect":{"percentage":{"value":10,"of":{"lines":{"where":{"gte":[{"fact":"line.quantity"},1]}}}}}}""",
            string.Empty));

        _ = await repository.ReplaceAsync(
            created with { DocumentJson = created.DocumentJson },
            created.ETag);

        var stale = created with { DocumentJson = created.DocumentJson + " " };

        await Assert.ThrowsAsync<PreconditionFailedException>(() =>
            repository.ReplaceAsync(stale, created.ETag));
    }
}
