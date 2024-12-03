
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AspireShop.Frontend.Services
{
    public class ChatServiceClient (HttpClient httpClient)
    {
        [Experimental("SKEXP0001")]
        public async Task<ChatService?> SendMessage(string message, ChatHistory? history = null, CancellationToken cancellationToken = default)
        {
            string url = "";
            if (history is not null)
            {
                history.AddUserMessage(message);
                
                // loop all messages in history and add to the user message
                for (int i=0; i<history.Count; i++)
                {
                    message += history[i].Content;
                }
            }
            url = $"/api/chat?message={Uri.EscapeDataString(message)}";
            var responseJson = await httpClient.GetAsync(url, cancellationToken);
            if (responseJson.IsSuccessStatusCode)
            {
                var responseContent = await responseJson.Content.ReadAsStringAsync();
                var chatService = JsonSerializer.Deserialize<ChatService>(responseContent);
                return chatService;
            }
            else
            {
                throw new HttpRequestException($"Request to {url} failed with status code {responseJson.StatusCode}");
            }
        }
        [Experimental("SKEXP0001")]
        public record ChatService (string message, ChatHistory history, string? intent = null, byte[]? audioContent = null);
    }
}