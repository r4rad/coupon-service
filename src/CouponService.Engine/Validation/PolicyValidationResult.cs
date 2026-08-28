namespace CouponService.Engine.Validation;

public sealed record PolicyValidationError(string Path, string Message);

public sealed class PolicyValidationResult(IReadOnlyList<PolicyValidationError> errors)
{
    public static PolicyValidationResult Valid { get; } = new([]);

    public IReadOnlyList<PolicyValidationError> Errors { get; } = errors;

    public bool IsValid => Errors.Count == 0;
}
