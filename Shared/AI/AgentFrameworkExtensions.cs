using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AspireShop.ServiceDefaults.AI;

public static class AgentFrameworkExtensions
{
    public static IServiceCollection AddChatAgent(
        this IServiceCollection services,
        string agentName,
        Action<IChatClient>? configureChatClient = null)
    {
        services.AddSingleton<IChatClientBuilder>(sp =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();
            configureChatClient?.Invoke(chatClient);
            return new ChatClientBuilder(chatClient);
        });

        services.AddSingleton<IChatAgent>(sp =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();
            return new ChatClientAgent(agentName, chatClient);
        });

        return services;
    }

    private class ChatClientBuilder : IChatClientBuilder
    {
        private readonly IChatClient _chatClient;

        public ChatClientBuilder(IChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public IChatClient Build() => _chatClient;
    }
}

public interface IChatClientBuilder
{
    IChatClient Build();
}

public interface IChatAgent
{
    string Name { get; }
    Task<ChatCompletion> CompleteAsync(IList<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default);
}

public class ChatClientAgent : IChatAgent
{
    private readonly IChatClient _chatClient;

    public ChatClientAgent(string name, IChatClient chatClient)
    {
        Name = name;
        _chatClient = chatClient;
    }

    public string Name { get; }

    public async Task<ChatCompletion> CompleteAsync(IList<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _chatClient.CompleteAsync(messages, options, cancellationToken);
    }
}
