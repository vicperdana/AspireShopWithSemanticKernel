using System.Text.Json;
using AspireShop.ChatService.Services;
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
        var prompts = LoadPrompts();
        var getIntent = LoadIntent();

        _kernel.Plugins.Add(prompts);
        _kernel.ImportPluginFromFunctions(getIntent.Name, getIntent.Description, new KernelFunction[] {getIntent});
        
        var intent = await GetIntent(message, getIntent);
        return await HandleIntent(intent, message);
    }
    
    private KernelPlugin LoadPrompts()
    {
        string promptsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "./Plugins/Prompts");

        if (!Directory.Exists(promptsDirectory))
        {
            throw new DirectoryNotFoundException($"The directory {promptsDirectory} does not exist.");
        }

        return _kernel.CreatePluginFromPromptDirectory(promptsDirectory);
    }

    private KernelFunction LoadIntent()
    {
        using StreamReader reader = new("Resources/getIntent.prompt.yaml");
        return _kernel.CreateFunctionFromPromptYaml(
            reader.ReadToEndAsync().Result,
            promptTemplateFactory: new HandlebarsPromptTemplateFactory()
        );
    }
    
    private List<string> CreateChoices()
    {
        return new List<string> {"AllItems", "Unrelated", "EndConversation", "FilterCatalogItem"};
    }
    
    private List<ChatHistory> CreateFewShotExamples()
    {
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
        return fewShotExamples;
    }
    
    private async Task<string> GetIntent(string message, KernelFunction getIntent)
    {
        var choices = CreateChoices();
        var fewShotExamples = CreateFewShotExamples();
        var history = new ChatHistory();

        var intent = await _kernel.InvokeAsync(
            getIntent,
            new KernelArguments
            {
                { "request", message },
                { "choices", choices },
                { "history", history },
                { "fewShotExamples", fewShotExamples }
            }
        );

        return intent.ToString();
    }
    
    private async Task<ChatService> HandleIntent(string intent, string message)
    {
        // Create a history object to keep track of the conversation
        var history = new ChatHistory();
        
        OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
        {
            MaxTokens = 200,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };
    
        // Handle different intents with corresponding responses or actions
        switch (intent)
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