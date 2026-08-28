var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Order API";
        document.Info.Version = "v1";
        document.Info.Description = "Checkout stand-in for the pizza ordering platform.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.MapOpenApi();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
