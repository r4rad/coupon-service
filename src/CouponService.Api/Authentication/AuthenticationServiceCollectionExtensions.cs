using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CouponService.Api.Authentication;

internal static class AuthenticationServiceCollectionExtensions
{
    internal static IServiceCollection AddCouponAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authOptions = configuration
            .GetSection(AuthenticationOptions.SectionName)
            .Get<AuthenticationOptions>() ?? new AuthenticationOptions();

        services.Configure<AuthenticationOptions>(configuration.GetSection(AuthenticationOptions.SectionName));

        TestTokenStartupGuard.EnsureAllowed(environment, authOptions);

        var registerTestToken = authOptions.TestToken.Enabled
            && TestTokenStartupGuard.IsTestTokenEnvironment(environment);

        var authenticationBuilder = services.AddAuthentication(options =>
        {
            if (registerTestToken)
            {
                options.DefaultAuthenticateScheme = TestTokenAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = TestTokenAuthenticationDefaults.AuthenticationScheme;
            }
            else
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }
        });

        authenticationBuilder.AddJwtBearer(
            JwtBearerDefaults.AuthenticationScheme,
            jwtOptions =>
            {
                jwtOptions.Authority = authOptions.Jwt.Authority;
                jwtOptions.Audience = authOptions.Jwt.Audience;
                // Entra app roles arrive in the "roles" claim; map them for RequireRole (AC-7.3 / AC-7.4 / AC-7.7).
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authOptions.Jwt.Issuer,
                    ValidateAudience = true,
                    // Both shapes: v1 echoes the Application ID URI, v2 always sends the client id.
                    ValidAudiences = authOptions.Jwt.ValidAudiences(),
                    RoleClaimType = "roles",
                };
                jwtOptions.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        TrustedRedeemPrincipalClaimsTransformation.Apply(
                            context.Principal,
                            authOptions.Jwt.TrustedRedeemPrincipalIds);
                        return Task.CompletedTask;
                    },
                };
            });

        if (registerTestToken)
        {
            var signingKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(authOptions.TestToken.SigningKey));

            authenticationBuilder.AddJwtBearer(
                TestTokenAuthenticationDefaults.AuthenticationScheme,
                jwtOptions =>
                {
                    jwtOptions.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = authOptions.TestToken.Issuer,
                        ValidateAudience = true,
                        ValidAudience = authOptions.TestToken.Audience,
                        ValidateLifetime = true,
                        IssuerSigningKey = signingKey,
                    };
                });
        }

        services.AddAuthorization(options => options.AddCouponAuthorizationPolicies());

        return services;
    }
}
