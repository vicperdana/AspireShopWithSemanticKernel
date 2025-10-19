using AspireShop.ChatService.Plugins;
using AspireShop.ChatService.Utilities;
using AspireShop.ChatService.Services;
using AspireShop.ServiceDefaults.AI;
using AspireShop.ServiceDefaults.RateLimiting;
using AspireShop.ServiceDefaults.Logging;
using AspireShop.ServiceDefaults.Stripe;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddHttpForwarderWithServiceDiscovery();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// Add Agent Framework Services using Azure OpenAI
builder.Services.AddOptions<AzureOpenAI>()
    .Bind(builder.Configuration.GetSection(nameof(AzureOpenAI)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Configure IChatClient for Microsoft.Extensions.AI using Azure OpenAI
// Also register Azure ChatClient for ChatAgentAdapter (temporary until Microsoft.Extensions.AI stabilizes)
builder.Services.AddSingleton<ChatClient>(sp =>
{
    AzureOpenAI options = sp.GetRequiredService<IOptions<AzureOpenAI>>().Value;
    var azureClient = new AzureOpenAIClient(new Uri(options.Endpoint), new AzureKeyCredential(options.ApiKey));
    return azureClient.GetChatClient(options.ChatDeploymentName);
});

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var azureChat = sp.GetRequiredService<ChatClient>();
    // Convert Azure.AI.OpenAI ChatClient to Microsoft.Extensions.AI.IChatClient
    return azureChat.AsIChatClient();
});

// Add throttling services
builder.Services.AddSingleton<ChatThrottleOptions>(sp =>
{
    var options = new ChatThrottleOptions();
    builder.Configuration.GetSection("ChatThrottle").Bind(options);
    return options;
});
builder.Services.AddSingleton<IChatThrottle, InMemoryChatThrottle>();

// Add Stripe configuration
builder.Services.AddOptions<StripeSettings>()
    .Bind(builder.Configuration.GetSection("Stripe"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register Stripe client factory
builder.Services.AddSingleton<StripeClientFactory>();

// Register background services
builder.Services.AddHostedService<ExpirationSweepService>();

// Register ChatAgentAdapter
builder.Services.AddSingleton<ChatAgentAdapter>();

/* Add Agent Framework Services using OpenAI
builder.Services.AddOptions<OpenAI>()
    .Bind(builder.Configuration.GetSection(nameof(OpenAI)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IChatClient>(sp =>
{
    OpenAI options = sp.GetRequiredService<IOptions<OpenAI>>().Value;
    var client = new OpenAIClient(options.ApiKey);
    return client.AsChatClient(options.ChatModelId);
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

// Add Chat Agent
builder.Services.AddSingleton<IChatAgent>(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    return new ChatClientAgent("AspireShopAgent", chatClient);
});

// Log migration started
var logger = builder.Logging.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
logger.LogAgentFrameworkMigrationStarted("ChatService");

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