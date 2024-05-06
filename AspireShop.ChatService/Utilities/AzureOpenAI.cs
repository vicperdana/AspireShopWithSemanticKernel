using System.ComponentModel.DataAnnotations;
namespace AspireShop.ChatService.Utilities;
/// <summary>
/// Azure OpenAI settings.
/// </summary>
public sealed class AzureOpenAI
{
    [Required]
    public string ChatDeploymentName { get; set; } = string.Empty;

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;
}