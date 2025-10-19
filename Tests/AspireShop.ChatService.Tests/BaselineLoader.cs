using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tests.AspireShop.ChatService.Tests;

public class BaselineLoader
{
    private readonly string _baselinePath;

    public BaselineLoader(string baselinePath = "Tests/baseline/chat-baseline.json")
    {
        _baselinePath = baselinePath ?? throw new ArgumentNullException(nameof(baselinePath));
    }

    public async Task<ChatBaseline> LoadBaselineAsync(CancellationToken cancellationToken = default)
    {
        var projectRoot = GetProjectRoot();
        var fullPath = Path.Combine(projectRoot, _baselinePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Baseline file not found: {fullPath}");
        }

        var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
        var baseline = JsonSerializer.Deserialize<ChatBaseline>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return baseline ?? throw new InvalidOperationException("Failed to deserialize baseline file");
    }

    private static string GetProjectRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (directory != null && !File.Exists(Path.Combine(directory, "AspireShop.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        if (directory == null)
        {
            throw new InvalidOperationException("Could not find solution root directory");
        }

        return directory;
    }
}

public class ChatBaseline
{
    [JsonPropertyName("baseline_version")]
    public string BaselineVersion { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("migration_target")]
    public string MigrationTarget { get; set; } = string.Empty;

    [JsonPropertyName("test_scenarios")]
    public List<ChatTestScenario> TestScenarios { get; set; } = new();

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class ChatTestScenario
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("user_query")]
    public string UserQuery { get; set; } = string.Empty;

    [JsonPropertyName("expected_response_contains")]
    public List<string> ExpectedResponseContains { get; set; } = new();

    [JsonPropertyName("expected_response_min_length")]
    public int ExpectedResponseMinLength { get; set; }
}
