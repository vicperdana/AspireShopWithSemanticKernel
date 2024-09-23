using AspireShop.ChatService.Plugins;
using Microsoft.SemanticKernel;
using AspireShop.ChatService.Utilities;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
using AspireShop.ChatService.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

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

builder.Services.AddHttpServiceReference<CatalogChatClient>("https+http://catalogservice", healthRelativePath: "health");

builder.Services.AddKeyedSingleton<FilterCatalogItem>("FilterCatalogItem", (Func<IServiceProvider, object, FilterCatalogItem>) ((sp, key) =>
{
    var catalogClientChatService = sp.GetRequiredService<CatalogChatClient>();
    if (catalogClientChatService is null)
    {
        throw new InvalidOperationException("CatalogChatClient is not registered in the service provider.");
    }
    return new FilterCatalogItem(catalogClientChatService);
}));

builder.Services.AddKeyedTransient<Kernel>("AspireShopKernel", (sp, key) =>
{
    // Create a collection of plugins that the kernel will use
    KernelPluginCollection pluginCollection = [];
    pluginCollection.AddFromObject(sp.GetRequiredKeyedService<FilterCatalogItem>("FilterCatalogItem"), "FilterCatalogItem");
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