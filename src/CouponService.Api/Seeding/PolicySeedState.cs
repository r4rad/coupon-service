namespace CouponService.Api.Seeding;

public enum PolicySeedStatus
{
    Pending,
    Disabled,
    Succeeded,
    Failed,
}

/// <summary>
/// Outcome of the startup seed, surfaced through the readiness probe so a deployment can be
/// verified without an admin token.
/// </summary>
public sealed class PolicySeedState
{
    private readonly object _gate = new();

    private PolicySeedStatus _status = PolicySeedStatus.Pending;
    private PolicySeedReport? _report;
    private string? _error;

    public (PolicySeedStatus Status, PolicySeedReport? Report, string? Error) Read()
    {
        lock (_gate)
        {
            return (_status, _report, _error);
        }
    }

    public void MarkDisabled()
    {
        lock (_gate)
        {
            _status = PolicySeedStatus.Disabled;
        }
    }

    public void MarkSucceeded(PolicySeedReport report)
    {
        lock (_gate)
        {
            _status = PolicySeedStatus.Succeeded;
            _report = report;
            _error = null;
        }
    }

    public void MarkFailed(string error)
    {
        lock (_gate)
        {
            _status = PolicySeedStatus.Failed;
            _error = error;
        }
    }
}
