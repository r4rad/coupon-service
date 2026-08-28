namespace CouponService.Infrastructure.InMemory;

public sealed class PreconditionFailedException : Exception
{
    public PreconditionFailedException()
        : base("The supplied ETag does not match the current entity version.")
    {
    }
}
