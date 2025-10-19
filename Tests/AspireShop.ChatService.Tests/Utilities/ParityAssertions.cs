namespace Tests.AspireShop.ChatService.Tests.Utilities;

/// <summary>
/// Assertion helpers for validating parity between Semantic Kernel baseline and Agent Framework responses
/// </summary>
public static class ParityAssertions
{
    /// <summary>
    /// Validates that a response contains all expected keywords (case-insensitive)
    /// </summary>
    public static void AssertContainsKeywords(string response, IEnumerable<string> expectedKeywords, string? because = null)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Response cannot be null or whitespace", nameof(response));
        }

        var missing = new List<string>();
        var responseLower = response.ToLowerInvariant();

        foreach (var keyword in expectedKeywords)
        {
            if (!responseLower.Contains(keyword.ToLowerInvariant()))
            {
                missing.Add(keyword);
            }
        }

        if (missing.Any())
        {
            var message = $"Response missing expected keywords: {string.Join(", ", missing)}";
            if (!string.IsNullOrWhiteSpace(because))
            {
                message += $" because {because}";
            }
            throw new Xunit.Sdk.XunitException(message);
        }
    }

    /// <summary>
    /// Validates that a response meets minimum length requirement
    /// </summary>
    public static void AssertMinimumLength(string response, int minLength, string? because = null)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Response cannot be null or whitespace", nameof(response));
        }

        if (response.Length < minLength)
        {
            var message = $"Response length ({response.Length}) is less than minimum required ({minLength})";
            if (!string.IsNullOrWhiteSpace(because))
            {
                message += $" because {because}";
            }
            throw new Xunit.Sdk.XunitException(message);
        }
    }

    /// <summary>
    /// Calculates Jaccard similarity coefficient between two sets of words
    /// Used for semantic similarity comparison (0.0 = no overlap, 1.0 = identical)
    /// </summary>
    public static double CalculateJaccardSimilarity(string text1, string text2)
    {
        if (string.IsNullOrWhiteSpace(text1) && string.IsNullOrWhiteSpace(text2))
        {
            return 1.0; // Both empty = identical
        }

        if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
        {
            return 0.0; // One empty = no overlap
        }

        var words1 = ExtractWords(text1);
        var words2 = ExtractWords(text2);

        var intersection = words1.Intersect(words2, StringComparer.OrdinalIgnoreCase).Count();
        var union = words1.Union(words2, StringComparer.OrdinalIgnoreCase).Count();

        return union == 0 ? 0.0 : (double)intersection / union;
    }

    /// <summary>
    /// Asserts that two responses have semantic similarity above a threshold
    /// Default threshold is 0.3 (30% word overlap)
    /// </summary>
    public static void AssertSemanticSimilarity(
        string baselineResponse,
        string actualResponse,
        double minSimilarity = 0.3,
        string? because = null)
    {
        var similarity = CalculateJaccardSimilarity(baselineResponse, actualResponse);

        if (similarity < minSimilarity)
        {
            var message = $"Semantic similarity ({similarity:P1}) is below threshold ({minSimilarity:P1})";
            if (!string.IsNullOrWhiteSpace(because))
            {
                message += $" because {because}";
            }
            message += $"\nBaseline: {TruncateForDisplay(baselineResponse)}";
            message += $"\nActual: {TruncateForDisplay(actualResponse)}";
            throw new Xunit.Sdk.XunitException(message);
        }
    }

    /// <summary>
    /// Asserts that response time is within acceptable range (≤2x baseline or absolute max)
    /// </summary>
    public static void AssertResponseTime(
        TimeSpan actualTime,
        TimeSpan? baselineTime = null,
        TimeSpan? absoluteMaxTime = null,
        string? because = null)
    {
        TimeSpan maxAllowed;

        if (baselineTime.HasValue)
        {
            // Allow 2x baseline time for regression tolerance
            maxAllowed = TimeSpan.FromMilliseconds(baselineTime.Value.TotalMilliseconds * 2);
        }
        else if (absoluteMaxTime.HasValue)
        {
            maxAllowed = absoluteMaxTime.Value;
        }
        else
        {
            // Default: 5 seconds for chat responses
            maxAllowed = TimeSpan.FromSeconds(5);
        }

        if (actualTime > maxAllowed)
        {
            var message = $"Response time ({actualTime.TotalMilliseconds:F0}ms) exceeds maximum allowed ({maxAllowed.TotalMilliseconds:F0}ms)";
            if (!string.IsNullOrWhiteSpace(because))
            {
                message += $" because {because}";
            }
            throw new Xunit.Sdk.XunitException(message);
        }
    }

    /// <summary>
    /// Validates that a response is coherent (not truncated, has sentence structure)
    /// </summary>
    public static void AssertCoherence(string response, string? because = null)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new ArgumentException("Response cannot be null or whitespace", nameof(response));
        }

        var issues = new List<string>();

        // Check for sentence-ending punctuation
        var trimmed = response.TrimEnd();
        if (!trimmed.EndsWith('.') && !trimmed.EndsWith('!') && !trimmed.EndsWith('?'))
        {
            issues.Add("response appears truncated (no sentence-ending punctuation)");
        }

        // Check for minimum word count (at least 5 words for coherent response)
        var wordCount = ExtractWords(response).Length;
        if (wordCount < 5)
        {
            issues.Add($"response too short ({wordCount} words) for coherent answer");
        }

        if (issues.Any())
        {
            var message = $"Response coherence issues: {string.Join("; ", issues)}";
            if (!string.IsNullOrWhiteSpace(because))
            {
                message += $" because {because}";
            }
            message += $"\nResponse: {TruncateForDisplay(response)}";
            throw new Xunit.Sdk.XunitException(message);
        }
    }

    private static string[] ExtractWords(string text)
    {
        return text
            .Split(new[] { ' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':', '-', '(', ')', '[', ']', '{', '}' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2) // Filter out short words (a, an, is, etc.)
            .ToArray();
    }

    private static string TruncateForDisplay(string text, int maxLength = 100)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength) + "...";
    }
}
