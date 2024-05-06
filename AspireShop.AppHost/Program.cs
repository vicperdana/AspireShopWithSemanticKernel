var builder = DistributedApplication.CreateBuilder(args);


var catalogDb = builder.AddPostgres("catalog", password: builder.CreateStablePassword("catalog-password"));
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

// Azure OpenAI
var chatDeploymentName = builder.AddParameter("chatDeploymentName", secret: true);
var chatEndpoint = builder.AddParameter("chatEndpoint", secret: true);
var chatApiKey = builder.AddParameter("chatApiKey", secret: true);
var chatService = builder.AddProject<Projects.AspireShop_ChatService>("chatservice")
    .WithEnvironment("AzureOpenAI__ChatDeploymentName", chatDeploymentName)
    .WithEnvironment("AzureOpenAI__Endpoint", chatEndpoint)
    .WithEnvironment("AzureOpenAI__ApiKey", chatApiKey)
    .WithReference(catalogService)
    .WithReference(postgres);

/* OpenAI
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