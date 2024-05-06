// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;

namespace AspireShop.ChatService.Utilities;

/// <summary>
/// OpenAI settings.
/// </summary>
public sealed class OpenAI
{
    [Required]
    public string ChatModelId { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;
}