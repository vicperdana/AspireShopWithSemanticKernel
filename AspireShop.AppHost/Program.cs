var builder = DistributedApplication.CreateBuilder(args);


var catalogDb = builder.AddPostgres("catalog", password: builder.CreateStablePassword("catalog-password"))
                                                    .WithPgAdmin();

if (builder.ExecutionContext.IsRunMode)
{
    catalogDb.WithDataVolume();
}

var postgres = catalogDb.AddDatabase("catalogdb");

var basketCache = builder.AddRedis("basketcache")
    .WithRedisCommander();
    
if (builder.ExecutionContext.IsRunMode)
{
    basketCache.WithDataVolume();
}

var catalogService = builder.AddProject<Projects.AspireShop_CatalogService>("catalogservice")
    .WithReference(postgres);

var basketService = builder.AddProject<Projects.AspireShop_BasketService>("basketservice")
    .WithReference(basketCache);


// Enable the Embedding service to use Azure OpenAI
var embedDeploymentName = builder.AddParameter("embedDeploymentName", secret: true);
var embedEndpoint = builder.AddParameter("embedEndpoint", secret: true);
var embedApiKey = builder.AddParameter("embedApiKey", secret: true);

// Embedding generation and indexing
//var apiKey = builder.AddParameter("apikey", secret: true);
var qdrant = builder.AddQdrant("qdrant");

var dataEmbedder = builder.AddProject<Projects.AspireShop_DataEmbedder>("dataembedder")
    .WithEnvironment("AzureOpenAI__EmbedDeploymentName", embedDeploymentName)
    .WithEnvironment("AzureOpenAI__EmbedEndpoint", embedEndpoint)
    .WithEnvironment("AzureOpenAI__EmbedApiKey", embedApiKey)
    .WithReference(postgres)
    .WithReference(qdrant);

// Enable the chat service to use Azure OpenAI
var chatDeploymentName = builder.AddParameter("chatDeploymentName", secret: true);
var chatEndpoint = builder.AddParameter("chatEndpoint", secret: true);
var chatApiKey = builder.AddParameter("chatApiKey", secret: true);

// Without Vector Search
/*var chatService = builder.AddProject<Projects.AspireShop_ChatService>("chatservice")
    .WithEnvironment("AzureOpenAI__ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AzureOpenAI__Endpoint", chatEndpoint)
    .WithEnvironment("AzureOpenAI__ApiKey", chatApiKey)
    .WithReference(catalogService)
    .WithReference(postgres);*/

//With Vector Search
var chatService = builder.AddProject<Projects.AspireShop_ChatService>("chatservice")
    .WithEnvironment("AzureOpenAI__ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AzureOpenAI__Endpoint", chatEndpoint)
    .WithEnvironment("AzureOpenAI__ApiKey", chatApiKey)
    .WithEnvironment("AzureOpenAI__EmbedDeploymentName", embedDeploymentName)
    .WithEnvironment("AzureOpenAI__EmbedEndpoint", embedEndpoint)
    .WithEnvironment("AzureOpenAI__EmbedApiKey", embedApiKey)
    .WithReference(qdrant);

/* Enable the chat service to use OpenAI
 var chatModelId = builder.AddParameter("chatModelId", secret: true);
var chatApiKey = builder.AddParameter("chatApiKey", secret: true);
var chatService = builder.AddProject<Projects.AspireShop_ChatService>("chatservice")
    .WithEnvironment("OpenAI__ChatModelId", chatModelId)
    .WithEnvironment("OpenAI__ApiKey", chatApiKey)
    .WithReference(catalogService)
    .WithReference(postgres);
*/

builder.AddProject<Projects.AspireShop_Frontend>("frontend")
    .WithReference(basketService)
    .WithReference(catalogService)
    .WithReference(chatService)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.AspireShop_CatalogDbManager>("catalogdbmanager")
    .WithReference(postgres);

builder.Build().Run();