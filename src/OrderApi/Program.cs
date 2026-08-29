using OrderApi.Auth;
using OrderApi.Catalog;
using OrderApi.Clients;
using OrderApi.OpenApi;
using OrderApi.Options;
using OrderApi.Orders;
using OrderApi.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<OrderApiOptions>()
    .Bind(builder.Configuration.GetSection(OrderApiOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.CouponServiceBaseUrl), "CouponServiceBaseUrl is required")
    .Validate(
        options => !options.UseManagedIdentity || !string.IsNullOrWhiteSpace(options.CouponServiceResource),
        "CouponServiceResource is required when UseManagedIdentity is true")
    .ValidateOnStart();

builder.Services.AddSingleton<IPizzaCatalog, FilePizzaCatalog>();
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddHttpClient(ManagedIdentityCouponServiceTokenProvider.HttpClientName);

var useManagedIdentity = builder.Configuration.GetValue(
    $"{OrderApiOptions.SectionName}:UseManagedIdentity",
    false);
if (useManagedIdentity)
{
    builder.Services.AddSingleton<ICouponServiceTokenProvider, ManagedIdentityCouponServiceTokenProvider>();
}
else
{
    builder.Services.AddSingleton<ICouponServiceTokenProvider, ConfigurationCouponServiceTokenProvider>();
}

builder.Services.AddSingleton<IOrderCheckoutService, OrderCheckoutService>();

builder.Services.AddHttpClient<ICouponServiceClient, HttpCouponServiceClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OrderApiOptions>>().Value;
    client.BaseAddress = new Uri(options.CouponServiceBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(3);
});

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

public partial class Program;
