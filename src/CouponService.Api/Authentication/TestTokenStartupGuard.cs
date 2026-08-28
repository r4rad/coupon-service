namespace CouponService.Api.Authentication;

// AC-7.5 / P-8: configuration alone must not enable the test scheme outside Development or Test.
internal static class TestTokenStartupGuard
{
    internal static void EnsureAllowed(IHostEnvironment environment, AuthenticationOptions options)
    {
        if (!options.TestToken.Enabled)
        {
            return;
        }

        if (!IsTestTokenEnvironment(environment))
        {
            throw new InvalidOperationException(
                "Test token authentication is enabled in configuration but the hosting environment is " +
                $"'{environment.EnvironmentName}'. Test tokens are permitted only in Development or Test.");
        }
    }

    internal static bool IsTestTokenEnvironment(IHostEnvironment environment) =>
        environment.IsDevelopment()
        || environment.IsEnvironment("Test")
        || environment.IsEnvironment("Testing");
}
