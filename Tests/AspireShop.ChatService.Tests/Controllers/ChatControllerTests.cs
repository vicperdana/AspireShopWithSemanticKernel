
using AspireShop.ChatService.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;
using Moq;

namespace Tests.AspireShop.ChatService.Tests.Controllers;
public class ChatControllerTests
{
    [Fact]
    public async Task PostMessage_ReturnsExpectedResult()
    {
        // Arrange
        var mockChatCompletion = new Mock<IChatCompletionService>(); 
        mockChatCompletion 
            .Setup(x => x.GetChatMessageContentsAsync( 
                It.IsAny<ChatHistory>(), 
                It.IsAny<PromptExecutionSettings>(), 
                It.IsAny<Kernel>(), 
                It.IsAny<CancellationToken>())) 
            .ReturnsAsync([new ChatMessageContent(AuthorRole.Assistant, "AI response")]); 

        var mockTextGenerationService = new Mock<ITextGenerationService>();
        var kernelBuilder = Kernel.CreateBuilder(); 
        kernelBuilder.Services.AddSingleton(mockChatCompletion.Object); 
        kernelBuilder.Services.AddSingleton(mockTextGenerationService.Object);
        var kernel = kernelBuilder.Build(); 
        
       
        
        var mockHostApplicationLifetime = new Mock<IHostApplicationLifetime>();
        var chatController = new ChatController(mockHostApplicationLifetime.Object, kernel);

        // Act
        var result = await chatController.PostMessage("Test Message");

        // Assert
        Assert.Equal("AI response", result.Value!.message);
    }
}