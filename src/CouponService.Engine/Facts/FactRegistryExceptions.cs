namespace CouponService.Engine.Facts;

public sealed class DuplicateFactRegistrationException(string path)
    : Exception($"Fact '{path}' is already registered.");

public sealed class UnknownFactException(string path)
    : Exception($"Fact '{path}' is not registered.");
