using AspireShop.ChatService.Plugins;
using Microsoft.SemanticKernel;
using AspireShop.ChatService.Utilities;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
using AspireShop.ChatService.Services;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.TextToAudio;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// Add the Qdrant client
builder.AddQdrantClient("qdrant");

builder.Services.AddHttpForwarderWithServiceDiscovery();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();


//Add Semantic Kernel Services using Azure OpenAI
builder.Services.AddOptions<AzureOpenAI>()
    .Bind(builder.Configuration.GetSection(nameof(AzureOpenAI)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Chat completion service that kernels will use
builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    AzureOpenAI options = sp.GetRequiredService<IOptions<AzureOpenAI>>().Value;
    return new AzureOpenAIChatCompletionService(options.ChatDeploymentName, options.Endpoint, options.ApiKey);
});
#pragma warning disable
builder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
{
    AzureOpenAI options = sp.GetRequiredService<IOptions<AzureOpenAI>>().Value;
    return new AzureOpenAITextEmbeddingGenerationService(options.EmbedDeploymentName, options.Endpoint, options.ApiKey);
});

/*builder.Services.AddSingleton<ITextToAudioService>(sp =>
{
    AzureOpenAI options = sp.GetRequiredService<IOptions<AzureOpenAI>>().Value;
    return new AzureOpenAITextToAudioService(options.VoiceDeploymentName, options.VoiceEndpoint, options.VoiceApiKey);
});*/
#pragma warning restore 
    
/* Add Semantic Kernel Services using OpenAI
builder.Services.AddOptions<OpenAI>()
    .Bind(builder.Configuration.GetSection(nameof(OpenAI)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Chat completion service that kernels will use
builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    OpenAI options = sp.GetRequiredService<IOptions<OpenAI>>().Value;
    return new OpenAIChatCompletionService(options.ChatModelId, options.ApiKey);
});*/

//builder.Services.AddHttpServiceReference<CatalogChatClient>("https+http://catalogservice", healthRelativePath: "health");
#pragma warning disable SKEXP0010
builder.Services.AddSingleton<CatalogChatClientVector>(sp =>
{
    var qdrantClient = sp.GetRequiredService<QdrantClient>();
    var EmbedDeploymentName = Environment.GetEnvironmentVariable("AzureOpenAI__EmbedDeploymentName")
                               ?? throw new ArgumentException("Environment variable 'AzureOpenAI__EmbedDeploymentName' is not set.");
    var Endpoint = Environment.GetEnvironmentVariable("AzureOpenAI__EmbedEndpoint")
                            ?? throw new ArgumentException("Environment variable 'AzureOpenAI__EmbedEndpoint' is not set.");
    var ApiKey = Environment.GetEnvironmentVariable("AzureOpenAI__EmbedApiKey")
                     ?? throw new ArgumentException("Environment variable 'AzureOpenAI__EmbedApiKey' is not set.");
    var qdrantCollectionName = "catalog_items";

    return new CatalogChatClientVector(
#pragma warning restore SKEXP0010
        qdrantClient,
        EmbedDeploymentName,
        Endpoint,
        ApiKey,
        qdrantCollectionName
    );
});




/*builder.Services.AddKeyedSingleton<FilterCatalogItem>("FilterCatalogItem", (Func<IServiceProvider, object?, FilterCatalogItem>) ((sp, key) =>
{
    var catalogClientChatService = sp.GetRequiredService<CatalogChatClient>();
    if (catalogClientChatService is null)
    {
        throw new InvalidOperationException("CatalogChatClient is not registered in the service provider.");
    }
    return new FilterCatalogItem(catalogClientChatService);
}));*/

builder.Services.AddKeyedSingleton<FilterCatalogItemVector>("FilterCatalogItemVector", (Func<IServiceProvider, object?, FilterCatalogItemVector>) ((sp, key) =>
{   
    #pragma warning disable SKEXP0010
    var catalogClientChatServiceVector = sp.GetRequiredService<CatalogChatClientVector>();
    #pragma warning restore SKEXP0010
    if (catalogClientChatServiceVector is null)
    {
        throw new InvalidOperationException("catalogClientChatServiceVector is not registered in the service provider.");
    }
    return new FilterCatalogItemVector(catalogClientChatServiceVector);
}));

builder.Services.AddKeyedTransient<Kernel>("AspireShopKernel", (sp, key) =>
{
    // Create a collection of plugins that the kernel will use
    KernelPluginCollection pluginCollection = [];
    //pluginCollection.AddFromObject(sp.GetRequiredKeyedService<FilterCatalogItem>("FilterCatalogItem"), "FilterCatalogItem");
    pluginCollection.AddFromObject(sp.GetRequiredKeyedService<FilterCatalogItemVector>("FilterCatalogItemVector"), "FilterCatalogItemVector");
#pragma warning disable SKEXP0050
    pluginCollection.AddFromType<ConversationSummaryPlugin>();

    // When created by the dependency injection container, Semantic Kernel logging is included by default
    return new Kernel(sp, pluginCollection);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
}
else
{
    app.UseExceptionHandler();
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapDefaultEndpoints();
app.Run();