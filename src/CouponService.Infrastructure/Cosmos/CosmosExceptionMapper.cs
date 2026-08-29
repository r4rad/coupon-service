using System.Net;
using CouponService.Application.Redemption;
using Microsoft.Azure.Cosmos;

namespace CouponService.Infrastructure.Cosmos;

internal static class CosmosExceptionMapper
{
    internal static Exception Map(CosmosException exception)
    {
        if (exception.StatusCode is HttpStatusCode.PreconditionFailed)
        {
            return new PreconditionFailedException();
        }

        if (exception.StatusCode is HttpStatusCode.Conflict)
        {
            return new InvalidOperationException(exception.Message, exception);
        }

        if (exception.StatusCode is HttpStatusCode.NotFound)
        {
            return new KeyNotFoundException(exception.Message, exception);
        }

        return exception;
    }

    internal static void ThrowForBatchFailure(CosmosBatchResult batch)
    {
        if (batch.IsSuccessStatusCode)
        {
            return;
        }

        if (batch.StatusCode is (int)HttpStatusCode.PreconditionFailed
            || batch.Operations.Any(op => op.StatusCode is (int)HttpStatusCode.PreconditionFailed))
        {
            throw new PreconditionFailedException();
        }

        if (batch.StatusCode is (int)HttpStatusCode.Conflict
            || batch.Operations.Any(op => op.StatusCode is (int)HttpStatusCode.Conflict))
        {
            throw new InvalidOperationException(
                "Transactional batch failed with a conflict (unique key or create race).");
        }

        throw new InvalidOperationException(
            $"Transactional batch failed with status {batch.StatusCode}.");
    }
}
