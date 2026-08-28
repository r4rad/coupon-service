using OrderApi.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Order API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Checkout stand-in for the pizza ordering platform. " +
            "Interactive docs: /scalar (try it out) · /redoc (reference) · /openapi/v1.json.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapApiDocumentation("Order API");
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
