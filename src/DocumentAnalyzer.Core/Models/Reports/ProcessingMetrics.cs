using System.Text.Json.Serialization;

namespace DocumentAnalyzer.Core.Models.Reports;

/// <summary>What one model call cost. Fields that only some providers report are null for the others.</summary>
public sealed class ProcessingMetrics
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double TotalDurationSeconds { get; set; }
    public int PromptTokens { get; set; }
    public int GeneratedTokens { get; set; }
    public int PromptCharacters { get; set; }

    // Reported by Ollama; most cloud APIs expose only token counts.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ModelLoadSeconds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PromptEvaluationSeconds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GenerationSeconds { get; set; }
}
