using ResearchHub.Core.Models;
using System.Globalization;

namespace ResearchHub.Services;

public sealed class LlmScreeningSettings
{
    public string Endpoint { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "llama3";
    public string PromptTemplate { get; init; } = DefaultPromptTemplate;
    public int TimeoutSeconds { get; init; } = 30;

    public const string DefaultPromptTemplate =
        "You are assisting with systematic review screening.\n" +
        "Return ONLY valid JSON with fields: verdict, confidence, reason.\n" +
        "Allowed verdict values: include, exclude, maybe.\n" +
        "Use confidence between 0 and 1.\n" +
        "\n" +
        "Screening phase: {phase}\n" +
        "Inclusion/Exclusion criteria: {criteria}\n" +
        "\n" +
        "Title: {title}\n" +
        "Abstract: {abstract}\n";

    public static LlmScreeningSettings FromEnvironment()
    {
        return new LlmScreeningSettings
        {
            Endpoint = ReadEnv("RH_OLLAMA_ENDPOINT") ?? "http://localhost:11434",
            Model = ReadEnv("RH_OLLAMA_MODEL") ?? "llama3",
            PromptTemplate = ReadEnv("RH_OLLAMA_PROMPT") ?? DefaultPromptTemplate,
            TimeoutSeconds = ReadEnvInt("RH_OLLAMA_TIMEOUT_SECONDS") ?? 30
        };
    }

    private static string? ReadEnv(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ReadEnvInt(string key)
    {
        var value = ReadEnv(key);
        if (value == null)
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}

public sealed class LlmScreeningSuggestion
{
    public required ScreeningVerdict Verdict { get; init; }
    public double? Confidence { get; init; }
    public string? Reason { get; init; }
    public string? RawJson { get; init; }
}

public sealed class LlmScreeningResult
{
    public bool IsSuccess { get; init; }
    public LlmScreeningSuggestion? Suggestion { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RawResponse { get; init; }

    public static LlmScreeningResult Success(LlmScreeningSuggestion suggestion, string? rawResponse)
    {
        return new LlmScreeningResult
        {
            IsSuccess = true,
            Suggestion = suggestion,
            RawResponse = rawResponse
        };
    }

    public static LlmScreeningResult Error(string code, string message, string? rawResponse = null)
    {
        return new LlmScreeningResult
        {
            IsSuccess = false,
            ErrorCode = code,
            ErrorMessage = message,
            RawResponse = rawResponse
        };
    }
}
