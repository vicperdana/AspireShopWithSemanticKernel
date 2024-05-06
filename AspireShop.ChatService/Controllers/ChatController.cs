using System.Text.Json;
using AspireShop.ChatService.Services;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

namespace AspireShop.ChatService.Controllers;

public class ChatController(
    IHostApplicationLifetime hostApplicationLifetime,
    [FromKeyedServices("AspireShopKernel")]
    Kernel kernel) : Controller
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;
    private readonly Kernel _kernel = kernel;
    
    [HttpGet ("api/chat")]
    public async Task<ActionResult<ChatService>> PostMessage(string message)
    {
        //Load prompts
        string promptsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "./Plugins/Prompts");

        if (!Directory.Exists(promptsDirectory))
        {
            throw new DirectoryNotFoundException($"The directory {promptsDirectory} does not exist.");
        }

        var prompts = _kernel.CreatePluginFromPromptDirectory(promptsDirectory);
        
        //Load prompt from YAML
        using StreamReader reader = new("Resources/getIntent.prompt.yaml");
        KernelFunction getIntent = _kernel.CreateFunctionFromPromptYaml(
            await reader.ReadToEndAsync(),
            promptTemplateFactory: new HandlebarsPromptTemplateFactory()
        );
        
        _kernel.Plugins.Add(prompts);
        _kernel.ImportPluginFromFunctions(getIntent.Name, getIntent.Description, new KernelFunction[] {getIntent});
        
        // Create choices
        List<string> choices = ["AllItems", "Unrelated", "EndConversation", "FilterCatalogItem"];
        
        // Create few-shot examples
        List<ChatHistory> fewShotExamples =
        [
            [
                new ChatMessageContent(AuthorRole.User, "Show me all items"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "AllItems")
            ],
            [
                new ChatMessageContent(AuthorRole.User, "Show me all hoodies"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "FilterCatalogItem")
            ],
            [
                new ChatMessageContent(AuthorRole.User, "Show me all t-shirts"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "FilterCatalogItem")
            ],
            [
                new ChatMessageContent(AuthorRole.User, "That is all I need"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "EndConversation")
            ],
            [
                new ChatMessageContent(AuthorRole.User, "I am done shopping"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "EndConversation")
            ],
            [
                new ChatMessageContent(AuthorRole.User, "Google me a recipe for chocolate cake"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "Unrelated")
            ]
        ];
        
        // Create chat history
        ChatHistory history = [];
        
        OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
        {
            MaxTokens = 200,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };
        var intent = await _kernel.InvokeAsync(
            getIntent,
            new(openAIPromptExecutionSettings)
            {
                { "request", message },
                { "choices", choices },
                { "history", history },
                { "fewShotExamples", fewShotExamples }
            }
        );
        
        switch (intent.ToString())
        {
            case "EndConversation":
                string responseEnd = $"Thank you for shopping at Aspire Shop";
                history.AddAssistantMessage(responseEnd);
                return new ChatService(responseEnd, history);
            case "AllItems":
                string responseAll = $"Here are all items";
                history.AddAssistantMessage(responseAll);
                return new ChatService(responseAll, history, " ");
            case "Unrelated":
                string responseUnrelated = $"I'm sorry, I am unable to assist with that request. Please try again asking related to catalog items of Aspire Shop.";
                history.AddAssistantMessage(responseUnrelated);
                return new ChatService(responseUnrelated, history);
        }

        string systemPrompt =
            """
            You are an AI assistant that helps people find information from Aspire Shop. You can help users find products, get information about product and nothing else. Do not offer to buy products, add to cart, or any other actions. If you found catalog items, only mention the name and the price and not any other information. Do not say that there is no picture available or not as it is not relevant. If the user asks for items in plural, remove the 's' and search for the singular form.
            """;
             
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(message);
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletionService.GetChatMessageContentAsync(history,
            executionSettings: openAIPromptExecutionSettings, kernel: _kernel);
        history.AddAssistantMessage(result.Content!);
        CatalogItemsPage? catalogItemsPage = null;
        foreach (var chatHistoryItem in history)
        {
            if (chatHistoryItem.Role == AuthorRole.Tool)
            {
                if (chatHistoryItem.Content != null)
                    catalogItemsPage = JsonSerializer.Deserialize<CatalogItemsPage>(chatHistoryItem.Content);
            }
        }
        var chatServiceResult = new ChatService(result.Content ?? string.Empty, history, catalogItemsPage?.SearchText);
        return chatServiceResult;
    }
}

public record ChatService(string message, ChatHistory history, string? intent = "");

public record CatalogItemsPage(int FirstId, int NextId, bool IsLastPage, IEnumerable<CatalogItem> Data, string? SearchText = null);