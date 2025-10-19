using System.Text.Json;
using Xunit.Abstractions;

namespace AspireShop.ChatService.Tests.Tools;

/// <summary>
/// Utility for comparing pre-migration and post-migration chat baseline responses.
/// Generates detailed diff reports to validate migration parity.
/// </summary>
public class ChatDiff
{
    private readonly ITestOutputHelper _output;

    public ChatDiff(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Compares two baseline JSON files and generates a detailed diff report.
    /// </summary>
    public async Task<DiffReport> CompareBaselinesAsync(string preFilePath, string postFilePath)
    {
        var preJson = await File.ReadAllTextAsync(preFilePath);
        var postJson = await File.ReadAllTextAsync(postFilePath);

        var preBaseline = JsonSerializer.Deserialize<ChatBaseline>(preJson);
        var postBaseline = JsonSerializer.Deserialize<ChatBaseline>(postJson);

        if (preBaseline == null || postBaseline == null)
        {
            throw new InvalidOperationException("Failed to deserialize baseline files");
        }

        var report = new DiffReport
        {
            PreMigrationVersion = preBaseline.BaselineVersion,
            PostMigrationVersion = postBaseline.BaselineVersion,
            ComparisonDate = DateTime.UtcNow,
            ScenarioComparisons = new List<ScenarioComparison>()
        };

        // Compare each scenario
        foreach (var preScenario in preBaseline.TestScenarios)
        {
            var postScenario = postBaseline.TestScenarios.FirstOrDefault(s => s.Id == preScenario.Id);
            
            if (postScenario == null)
            {
                report.MissingScenarios.Add(preScenario.Id);
                continue;
            }

            var comparison = new ScenarioComparison
            {
                ScenarioId = preScenario.Id,
                Description = preScenario.Description,
                UserQuery = preScenario.UserQuery,
                ExpectedKeywordsMatch = CompareKeywords(
                    preScenario.ExpectedResponseContains, 
                    postScenario.ExpectedResponseContains),
                MinLengthDelta = postScenario.ExpectedResponseMinLength - preScenario.ExpectedResponseMinLength
            };

            report.ScenarioComparisons.Add(comparison);
        }

        // Calculate overall statistics
        report.TotalScenarios = preBaseline.TestScenarios.Count;
        report.MatchingScenarios = report.ScenarioComparisons.Count(c => c.ExpectedKeywordsMatch);
        report.ParityPercentage = report.TotalScenarios > 0 
            ? (double)report.MatchingScenarios / report.TotalScenarios * 100 
            : 0;

        return report;
    }

    /// <summary>
    /// Generates a human-readable diff report.
    /// </summary>
    public string GenerateReport(DiffReport diff)
    {
        var report = new System.Text.StringBuilder();
        
        report.AppendLine("=== Chat Migration Baseline Comparison ===");
        report.AppendLine($"Pre-Migration: {diff.PreMigrationVersion}");
        report.AppendLine($"Post-Migration: {diff.PostMigrationVersion}");
        report.AppendLine($"Comparison Date: {diff.ComparisonDate:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();
        
        report.AppendLine("=== Summary ===");
        report.AppendLine($"Total Scenarios: {diff.TotalScenarios}");
        report.AppendLine($"Matching Scenarios: {diff.MatchingScenarios}");
        report.AppendLine($"Parity Percentage: {diff.ParityPercentage:F2}%");
        report.AppendLine();

        if (diff.MissingScenarios.Any())
        {
            report.AppendLine("⚠️  Missing Scenarios:");
            foreach (var missing in diff.MissingScenarios)
            {
                report.AppendLine($"  - {missing}");
            }
            report.AppendLine();
        }

        report.AppendLine("=== Scenario Details ===");
        foreach (var scenario in diff.ScenarioComparisons)
        {
            var status = scenario.ExpectedKeywordsMatch ? "✓ PASS" : "✗ FAIL";
            report.AppendLine($"{status} {scenario.ScenarioId}: {scenario.Description}");
            report.AppendLine($"  Query: {scenario.UserQuery}");
            report.AppendLine($"  Keywords Match: {scenario.ExpectedKeywordsMatch}");
            report.AppendLine($"  Min Length Delta: {scenario.MinLengthDelta}");
            report.AppendLine();
        }

        if (diff.ParityPercentage >= 90)
        {
            report.AppendLine("✅ Migration parity achieved (≥90% match)");
        }
        else if (diff.ParityPercentage >= 80)
        {
            report.AppendLine("⚠️  Migration parity acceptable (80-90% match) - review differences");
        }
        else
        {
            report.AppendLine("❌ Migration parity failed (<80% match) - investigation required");
        }

        return report.ToString();
    }

    private bool CompareKeywords(List<string> pre, List<string> post)
    {
        if (pre.Count != post.Count) return false;
        
        var preSorted = pre.OrderBy(k => k).ToList();
        var postSorted = post.OrderBy(k => k).ToList();
        
        return preSorted.SequenceEqual(postSorted, StringComparer.OrdinalIgnoreCase);
    }
}

public class DiffReport
{
    public string PreMigrationVersion { get; set; } = string.Empty;
    public string PostMigrationVersion { get; set; } = string.Empty;
    public DateTime ComparisonDate { get; set; }
    public int TotalScenarios { get; set; }
    public int MatchingScenarios { get; set; }
    public double ParityPercentage { get; set; }
    public List<string> MissingScenarios { get; set; } = new();
    public List<ScenarioComparison> ScenarioComparisons { get; set; } = new();
}

public class ScenarioComparison
{
    public string ScenarioId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UserQuery { get; set; } = string.Empty;
    public bool ExpectedKeywordsMatch { get; set; }
    public int MinLengthDelta { get; set; }
}

public class ChatBaseline
{
    public string BaselineVersion { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string MigrationTarget { get; set; } = string.Empty;
    public List<ChatTestScenario> TestScenarios { get; set; } = new();
}

public class ChatTestScenario
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UserQuery { get; set; } = string.Empty;
    public List<string> ExpectedResponseContains { get; set; } = new();
    public int ExpectedResponseMinLength { get; set; }
}
