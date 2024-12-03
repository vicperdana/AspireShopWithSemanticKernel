using System.Diagnostics.CodeAnalysis;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextToAudio;

namespace AspireShop.ChatService.Plugins;

public class TextToAudio
{
    private const string TextToAudioModel = "tts-1";
#pragma warning disable SKEXP0001
    private readonly ITextToAudioService _textToAudioService;
#pragma warning restore SKEXP0001

    [Experimental("SKEXP0001")]
    public TextToAudio(ITextToAudioService textToAudioService)
    {
        _textToAudioService = textToAudioService;
    }

    [KernelFunction]
    [Experimental("SKEXP0001")]
    public async Task<AudioContent> ConvertTextToAudio(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        OpenAITextToAudioExecutionSettings executionSettings = new()
        {
            Voice = "alloy",
            ResponseFormat = "mp3",
            Speed = 1.0f
        };

        var audio = await _textToAudioService.GetAudioContentAsync(text, executionSettings);
        return audio;
    }
}