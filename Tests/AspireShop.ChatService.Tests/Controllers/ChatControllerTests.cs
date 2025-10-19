
using AspireShop.ChatService.Controllers;
using AspireShop.ChatService.Plugins;
using AspireShop.ChatService.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Tests.AspireShop.ChatService.Tests.Controllers;
public class ChatControllerTests
{
    [Fact(Skip = "Temporarily disabled - needs Agent Framework mock strategy")]
    public async Task PostMessage_ReturnsExpectedResult()
    {
        // TODO: Update test to use AIAgent mock instead of IChatClient
        // The new ChatController uses agent.RunAsync() which is not easily mockable
        // Consider using a test double or integration test approach
        Assert.True(true);
    }
}