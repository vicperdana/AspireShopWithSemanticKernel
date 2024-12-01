using Microsoft.Extensions.Diagnostics.HealthChecks;
using AspireShop.Frontend.Components;
using AspireShop.Frontend.Services;
using AspireShop.GrpcBasket;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpForwarderWithServiceDiscovery();

builder.Services.AddHttpServiceReference<CatalogServiceClient>("https+http://catalogservice", healthRelativePath: "health");
#pragma warning disable SKEXP0010
builder.Services.AddSingleton<CatalogServiceClientVector>(sp =>
{
    var qdrantClient = sp.GetRequiredService<QdrantClient>();
    var EmbedDeploymentName = Environment.GetEnvironmentVariable("AzureOpenAI__EmbedDeploymentName")
                              ?? throw new ArgumentException("Environment variable 'AzureOpenAI__EmbedDeploymentName' is not set.");
    var Endpoint = Environment.GetEnvironmentVariable("AzureOpenAI__EmbedEndpoint")
                   ?? throw new ArgumentException("Environment variable 'AzureOpenAI__EmbedEndpoint' is not set.");
    var ApiKey = Environment.GetEnvironmentVariable("AzureOpenAI__EmbedApiKey")
                 ?? throw new ArgumentException("Environment variable 'AzureOpenAI__EmbedApiKey' is not set.");
    var qdrantCollectionName = "catalog_items";

    return new CatalogServiceClientVector(
#pragma warning restore SKEXP0010
        qdrantClient,
        EmbedDeploymentName,
        Endpoint,
        ApiKey,
        qdrantCollectionName
    );
});

var isHttps = builder.Configuration["DOTNET_LAUNCH_PROFILE"] == "https";

builder.Services.AddSingleton<BasketServiceClient>()
    .AddGrpcServiceReference<Basket.BasketClient>($"{(isHttps ? "https" : "http")}://basketservice", failureStatus: HealthStatus.Degraded);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpServiceReference<ChatServiceClient>("https+http://chatservice", healthRelativePath: "health");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseStaticFiles();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapForwarder("/catalog/images/{id}", "https+http://catalogservice", "/api/v1/catalog/items/{id}/image");

app.MapDefaultEndpoints();

app.Run();