var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

app.MapOpenApi();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
