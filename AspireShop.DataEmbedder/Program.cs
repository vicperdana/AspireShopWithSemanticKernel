using AspireShop.CatalogDb;
using AspireShop.DataEmbedder;
using Npgsql;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Configuration.AddEnvironmentVariables();

builder.AddQdrantClient("qdrant");
builder.AddNpgsqlDataSource("catalogdb");

#pragma warning disable SKEXP0010
builder.Services.AddSingleton<CatalogDbEmbedder>(sp =>
{
    var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
    var qdrantClient = sp.GetRequiredService<QdrantClient>();
    var openAiDeploymentName = Environment.GetEnvironmentVariable("AzureOpenAI__EmbedDeploymentName")
                               ?? throw new ArgumentException("Environment variable 'AzureOpenAI__EmbedDeploymentName' is not set.");
    var openAiEndpoint = Environment.GetEnvironmentVariable("AzureOpenAI__EmbedEndpoint")
                         ?? throw new ArgumentException("Environment variable 'AzureOpenAI__EmbedEndpoint' is not set.");
    var openAIKey = Environment.GetEnvironmentVariable("AzureOpenAI__EmbedApiKey")
                    ?? throw new ArgumentException("Environment variable 'AzureOpenAI__EmbedApiKey' is not set.");


    return new(dataSource, qdrantClient, openAiDeploymentName, openAiEndpoint, openAIKey);
});
#pragma warning restore SKEXP0010

var app = builder.Build();

// Execute the IngestAndVectorizeDataAsync method during application startup
using (var scope = app.Services.CreateScope())
{
#pragma warning disable SKEXP0010
    var embedder = scope.ServiceProvider.GetRequiredService<CatalogDbEmbedder>();
#pragma warning restore SKEXP0010
    Console.WriteLine("Starting data ingestion and vectorization...");
    await embedder.IngestAndVectorizeDataAsync<ulong>();
    Console.WriteLine("Data ingestion and vectorization completed.");
}

app.Run();