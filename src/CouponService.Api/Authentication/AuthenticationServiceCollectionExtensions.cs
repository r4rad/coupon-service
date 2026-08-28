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
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authOptions.Jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = authOptions.Jwt.Audience,
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
