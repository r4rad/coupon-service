using CouponService.Bdd.Support;
using Reqnroll;
using Reqnroll.BoDi;

namespace CouponService.Bdd.Hooks;

[Binding]
public sealed class BddHooks
{
    private static BddHost? _host;

    private readonly IObjectContainer _container;

    public BddHooks(IObjectContainer container)
    {
        _container = container;
    }

    internal static BddHost Host =>
        _host ?? throw new InvalidOperationException("BDD host was not initialised.");

    [BeforeTestRun]
    public static async Task BeforeTestRunAsync()
    {
        _host = BddHost.CreateFromConfiguration();
        await _host.EnsureStartedAsync().ConfigureAwait(false);
    }

    [AfterTestRun]
    public static async Task AfterTestRunAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync().ConfigureAwait(false);
            _host = null;
        }
    }

    [BeforeScenario(Order = 0)]
    public void RegisterScenarioState()
    {
        _container.RegisterInstanceAs(new ScenarioState());
    }

    [BeforeScenario(Order = 1)]
    public async Task BeforeScenarioAsync()
    {
        await Host.EnsureStartedAsync().ConfigureAwait(false);
        // Default clock: Friday 2026-08-28 — scenarios that need Tuesday override explicitly.
        Host.Clock.Set(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
    }

    [AfterScenario]
    public async Task AfterScenarioAsync(ScenarioState state)
    {
        state.ReplaceJson(null);
        state.LastResponse?.Dispose();
        foreach (var response in state.ConcurrentResponses)
        {
            response.Dispose();
        }

        state.ConcurrentResponses.Clear();
        await Host.TeardownSeededPoliciesAsync().ConfigureAwait(false);
    }
}
