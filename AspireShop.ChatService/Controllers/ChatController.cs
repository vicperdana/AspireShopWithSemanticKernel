using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AspireShop.ChatService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using AspireShop.ChatService.Plugins;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.TextToAudio;

namespace AspireShop.ChatService.Controllers;

[Experimental("SKEXP0001")]
public class ChatController(
    IHostApplicationLifetime hostApplicationLifetime,
    [FromKeyedServices("AspireShopKernel")]
    Kernel kernel) : Controller
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;
    private readonly Kernel _kernel = kernel;

    [HttpGet("api/chat")]
    [Experimental("SKEXP0001")]
    public async Task<ActionResult<ChatServiceResult>> PostMessage(string message)
    {
        var prompts = LoadPrompts();
        var getIntent = LoadIntent();

        _kernel.Plugins.Add(prompts);
        _kernel.ImportPluginFromFunctions(getIntent.Name, getIntent.Description, new KernelFunction[] { getIntent });

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
        return new List<string>
            { "AllItems", "Unrelated", "EndConversation", /*"FilterCatalogItem"*/ "FilterCatalogItemVector" };
    }

    private List<ChatHistory> CreateFewShotExamples()
    {
        // Create few-shot examples
        List<ChatHistory> fewShotExamples = new()
        {
            new ChatHistory
            {
                new ChatMessageContent(AuthorRole.User, "Show me all items"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "AllItems")
            },
            new ChatHistory
            {
                new ChatMessageContent(AuthorRole.User, "Show me all hoodies"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                //new ChatMessageContent(AuthorRole.Assistant, "FilterCatalogItem")
                new ChatMessageContent(AuthorRole.Assistant, "FilterCatalogItemVector")
            },
            new ChatHistory
            {
                new ChatMessageContent(AuthorRole.User, "Show me all t-shirts"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                //new ChatMessageContent(AuthorRole.Assistant, "FilterCatalogItem")
                new ChatMessageContent(AuthorRole.Assistant, "FilterCatalogItemVector")
            },
            new ChatHistory
            {
                new ChatMessageContent(AuthorRole.User, "That is all I need"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "EndConversation")
            },
            new ChatHistory
            {
                new ChatMessageContent(AuthorRole.User, "I am done shopping"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "EndConversation")
            },
            new ChatHistory
            {
                new ChatMessageContent(AuthorRole.User, "Google me a recipe for chocolate cake"),
                new ChatMessageContent(AuthorRole.System, "Intent:"),
                new ChatMessageContent(AuthorRole.Assistant, "Unrelated")
            }
        };
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

    [Experimental("SKEXP0001")]
    private async Task<ChatServiceResult> HandleIntent(string intent, string message)
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
                return new ChatServiceResult(responseEnd, history);

            case "AllItems":
                string responseAll = $"Here are all the items";
                history.AddAssistantMessage(responseAll);
                return new ChatServiceResult(responseAll, history);

            case "Unrelated":
                string responseUnrelated = $"I'm sorry, I am unable to assist with that request. Please try again asking related to catalog items of Aspire Shop.";
                history.AddAssistantMessage(responseUnrelated);
                return new ChatServiceResult(responseUnrelated, history);
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
        var modelId = Environment.GetEnvironmentVariable("OpenAI__ModelId")
                             ?? throw new ArgumentException("Environment variable 'OpenAI__ModelId' is not set.");
        var apiKey = Environment.GetEnvironmentVariable("OpenAI__VoiceApiKey")
                     ?? throw new ArgumentException("Environment variable 'OpenAI__VoiceApiKey' is not set.");
        var kernel = Kernel.CreateBuilder()
            .AddOpenAITextToAudio(
                modelId,
                apiKey
            ).Build();
        var textToAudioService = kernel.GetRequiredService<ITextToAudioService>();
        var audio = await textToAudioService.GetAudioContentAsync(result.Content, new OpenAITextToAudioExecutionSettings()
        {
            Voice = "alloy",
            ResponseFormat = "mp3",
            Speed = 1.0f
        });

        var chatServiceResult =
            new ChatServiceResult(result.Content ?? string.Empty, history, audio.Data.Value.ToArray(), catalogItemsPage?.SearchText);
        return chatServiceResult;
    }
}

#pragma warning disable SKEXP0001
public record ChatServiceResult(string message, ChatHistory history, byte[] AudioContent = null, string? intent = "");

public record CatalogItemsPage(
    int FirstId,
    int NextId,
    bool IsLastPage,
    IEnumerable<CatalogItem> Data,
    byte[]? AudioContent = null,
    string? SearchText = null);