using CouponService.Api.DependencyInjection;
using CouponService.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
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

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Coupon Service API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Coupon validation, pricing preview, redemption lifecycle, and policy administration.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();

app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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
