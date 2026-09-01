using CouponService.Api.DependencyInjection;
using CouponService.Api.Observability;
using CouponService.Api.Authentication;
using CouponService.Api.Middleware;
using CouponService.Api.OpenApi;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddCouponSerilog();
builder.Services.AddCouponObservability();

builder.Services.AddControllers(options =>
    {
        options.Conventions.Add(new CouponAuthorizationConvention());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
                ?? context.HttpContext.TraceIdentifier;

            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            };

            problem.Extensions["correlationId"] = correlationId;
            return new BadRequestObjectResult(problem);
        };
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.HttpContext.TraceIdentifier;

        context.ProblemDetails.Extensions["correlationId"] = correlationId;
    };
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddCouponService(builder.Configuration);
}

builder.Services.AddCouponAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Coupon Service API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Coupon validation, pricing preview, redemption lifecycle, and policy administration. " +
            "Interactive docs: /scalar (try it out) · /redoc (reference) · /openapi/v1.json.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler();

app.UseStatusCodePages();

if (OpenApiUiExtensions.IsApiDocumentationEnabled(app.Environment, app.Configuration))
{
    app.MapApiDocumentation("Coupon Service API");
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/v1/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});

app.MapHealthChecks("/v1/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

app.Run();

public partial class Program;
