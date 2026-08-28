using Scalar.AspNetCore;

namespace CouponService.Api.OpenApi;

internal static class OpenApiUiExtensions
{
    internal static WebApplication MapApiDocumentation(this WebApplication app, string title)
    {
        app.MapOpenApi();

        app.MapScalarApiReference("/scalar", options =>
        {
            options
                .WithTitle(title)
                .WithOpenApiRoutePattern("/openapi/{documentName}.json");
        });

        app.MapGet("/redoc", () => Results.Content(BuildRedocHtml(title), "text/html; charset=utf-8"))
            .ExcludeFromDescription();

        return app;
    }

    private static string BuildRedocHtml(string title) =>
        $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>{{title}} — ReDoc</title>
          <style>
            body { margin: 0; padding: 0; }
          </style>
        </head>
        <body>
          <redoc spec-url="/openapi/v1.json"></redoc>
          <script src="https://cdn.redoc.ly/redoc/latest/bundles/redoc.standalone.js"></script>
        </body>
        </html>
        """;
}
