using OpenAI.Chat;

namespace AspireShop.ChatService.Services;

/// <summary>
/// Adapter for migrating from Semantic Kernel to Agent Framework.
/// 
/// NOTE: Currently still using Azure.AI.OpenAI.ChatClient directly while Microsoft.Extensions.AI
/// API stabilizes. The CompleteAsync/CompleteStreamingAsync extension methods are not yet accessible
/// in the current preview packages (version conflicts between Microsoft.Agents.AI 1.0.0-preview and
/// Microsoft.Extensions.AI 9.10.0-preview). 
/// 
/// TODO [T034]: Migrate to Microsoft.Extensions.AI.IChatClient once API is stable and accessible.
/// </summary>
public class ChatAgentAdapter
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<ChatAgentAdapter> _logger;

    public ChatAgentAdapter(ChatClient chatClient, ILogger<ChatAgentAdapter> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <summary>
    /// Executes a chat completion with system instructions and user message.
    /// </summary>
    public async Task<string> CompleteChatAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userMessage)
        };

        var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return response.Value.Content[0].Text ?? string.Empty;
    }

    /// <summary>
    /// Executes a chat completion with a list of chat messages.
    /// </summary>
    public async Task<string> CompleteChatAsync(IEnumerable<OpenAI.Chat.ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var response = await _chatClient.CompleteChatAsync(messages.ToList(), cancellationToken: cancellationToken);
        return response.Value.Content[0].Text ?? string.Empty;
    }

    /// <summary>
    /// Streams chat completion responses for real-time interaction.
    /// </summary>
    public async IAsyncEnumerable<string> StreamChatAsync(string systemPrompt, string userMessage, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userMessage)
        };

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return contentPart.Text;
                }
            }
        }
    }

    /// <summary>
    /// Determines user intent from message for routing decisions.
    /// </summary>
    public async Task<string> DetermineIntentAsync(string message, CancellationToken cancellationToken = default)
    {
        const string systemPrompt = """
            Classify the user's intent into one of these categories:
            - EndConversation: User wants to end the chat (e.g., "bye", "that's all")
            - AllItems: User wants to see all items (e.g., "show all items")
            - FilterCatalogItem: User wants to search for specific items
            - Unrelated: Question not related to shopping
            
            Respond with only the category name.
            """;

        try
        {
            var intent = await CompleteChatAsync(systemPrompt, message, cancellationToken);
            return intent.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to determine intent for message: {Message}", message);
            return "FilterCatalogItem"; // Default to catalog search
        }
    }
}
