using System.ComponentModel;
using System.Text.Json;
using System.Diagnostics;
using AspireShop.ChatService.Services;
using AspireShop.ChatService.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace AspireShop.ChatService.Controllers;

public class ChatController(
    IHostApplicationLifetime hostApplicationLifetime,
    ChatAgentAdapter chatAdapter,
    [FromKeyedServices("FilterCatalogItem")] FilterCatalogItem filterCatalogItem,
    ILogger<ChatController> logger) : Controller
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;
    private readonly ChatAgentAdapter _chatAdapter = chatAdapter;
    private readonly FilterCatalogItem _filterCatalogItem = filterCatalogItem;
    private readonly ILogger<ChatController> _logger = logger;
    private static readonly ActivitySource ActivitySource = new("AspireShop.ChatService");
    
    [HttpGet("api/chat")]
    public async Task<ActionResult<ChatResponse>> PostMessage(string message)
    {
        using var activity = ActivitySource.StartActivity("ChatController.PostMessage");
        activity?.SetTag("chat.message.length", message.Length);
        
        try
        {
            // Determine intent first (simplified approach)
            var intent = await DetermineIntent(message);
            activity?.SetTag("chat.intent", intent);
            
            switch (intent)
            {
                case "EndConversation":
                    activity?.SetTag("chat.response.type", "farewell");
                    return Ok(new ChatResponse("Thank you for shopping at Aspire Shop", null, intent));

                case "AllItems":
                    activity?.SetTag("chat.response.type", "all_items");
                    return Ok(new ChatResponse("Here are all items", null, "AllItems"));

                case "Unrelated":
                    activity?.SetTag("chat.response.type", "unrelated");
                    return Ok(new ChatResponse(
                        "I'm sorry, I can only assist with Aspire Shop catalog items. Please try again.",
                        null,
                        intent));
            }
            
            // For catalog queries, use chat client with system instructions
            const string systemPrompt = """
                You are an AI assistant for Aspire Shop that helps people find catalog items.
                You can help users search for products and get information about them.
                Do not offer to buy products or add to cart - only provide information.
                When showing catalog items, mention the name and price only.
                If users ask for items in plural, search for the singular form.
                """;
            
            var responseText = await _chatAdapter.CompleteChatAsync(systemPrompt, message);
            
            activity?.SetTag("chat.response.length", responseText.Length);
            activity?.SetTag("chat.response.type", "catalog_query");
            
            _logger.LogInformation("Chat request processed: Intent={Intent}, ResponseLength={Length}", 
                intent, responseText.Length);
            
            // For now, return simple response without tool invocation
            // TODO: Implement proper tool calling pattern when Microsoft.Agents.AI API is stable
            return Ok(new ChatResponse(responseText, null, "FilterCatalogItem"));
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", 
                tags: new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                    { "exception.stacktrace", ex.StackTrace }
                }));
            _logger.LogError(ex, "Error processing chat message");
            throw;
        }
    }
    
    private async Task<string> DetermineIntent(string message)
    {
        // Use ChatAgentAdapter for intent classification
        return await _chatAdapter.DetermineIntentAsync(message);
    }
}

public record ChatResponse(string Message, CatalogItemsPage? CatalogItems, string? Intent = null);

public record CatalogItemsPage(int FirstId, int NextId, bool IsLastPage, IEnumerable<CatalogItem> Data, string? SearchText = null);

public record CatalogItem(int Id, string Name, decimal Price);
