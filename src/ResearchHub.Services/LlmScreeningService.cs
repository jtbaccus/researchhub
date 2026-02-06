using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ResearchHub.Core.Models;

namespace ResearchHub.Services;

public sealed class LlmScreeningService : ILlmScreeningService
{
    private readonly HttpClient _httpClient;
    private readonly LlmScreeningSettings _settings;

    public LlmScreeningService(HttpClient httpClient, LlmScreeningSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
        if (_httpClient.Timeout == Timeout.InfiniteTimeSpan)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        }
    }

    public async Task<LlmScreeningResult> SuggestAsync(
        Reference reference,
        ScreeningPhase phase,
        string? criteria = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reference.Title) && string.IsNullOrWhiteSpace(reference.Abstract))
        {
            return LlmScreeningResult.Error(
                "missing_input",
                "Reference is missing both title and abstract.");
        }

        var endpoint = BuildEndpoint(_settings.Endpoint);
        var prompt = BuildPrompt(reference, phase, criteria, _settings.PromptTemplate);

        var requestBody = new
        {
            model = _settings.Model,
            prompt,
            stream = false
        };

        HttpResponseMessage? response = null;
        string? responseBody = null;

        try
        {
            response = await _httpClient.PostAsJsonAsync(
                endpoint,
                requestBody,
                cancellationToken);

            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return HandleErrorStatus(response.StatusCode, responseBody);
            }

            return ParseOllamaResponse(responseBody);
        }
        catch (HttpRequestException ex)
        {
            return LlmScreeningResult.Error(
                "ollama_unavailable",
                $"Unable to reach Ollama at {_settings.Endpoint}: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return LlmScreeningResult.Error(
                "ollama_timeout",
                $"Ollama request timed out after {_settings.TimeoutSeconds} seconds.");
        }
        catch (JsonException ex)
        {
            return LlmScreeningResult.Error(
                "invalid_response",
                $"Failed to parse Ollama response JSON: {ex.Message}",
                responseBody);
        }
    }

    private static string BuildEndpoint(string baseEndpoint)
    {
        var trimmed = baseEndpoint.TrimEnd('/');
        return $"{trimmed}/api/generate";
    }

    private static string BuildPrompt(
        Reference reference,
        ScreeningPhase phase,
        string? criteria,
        string template)
    {
        var safeCriteria = string.IsNullOrWhiteSpace(criteria) ? "None provided." : criteria.Trim();
        var safeAbstract = string.IsNullOrWhiteSpace(reference.Abstract) ? "None provided." : reference.Abstract.Trim();

        return template
            .Replace("{title}", reference.Title?.Trim() ?? string.Empty, StringComparison.Ordinal)
            .Replace("{abstract}", safeAbstract, StringComparison.Ordinal)
            .Replace("{criteria}", safeCriteria, StringComparison.Ordinal)
            .Replace("{phase}", phase.ToString(), StringComparison.Ordinal);
    }

    private static LlmScreeningResult HandleErrorStatus(HttpStatusCode statusCode, string? responseBody)
    {
        var body = responseBody ?? string.Empty;
        if (statusCode == HttpStatusCode.NotFound ||
            body.Contains("model", StringComparison.OrdinalIgnoreCase) &&
            body.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return LlmScreeningResult.Error(
                "model_not_found",
                "Ollama reported that the model is not available. Pull the model with `ollama pull`.",
                responseBody);
        }

        return LlmScreeningResult.Error(
            "ollama_error",
            $"Ollama returned {(int)statusCode} {statusCode}.",
            responseBody);
    }

    private static LlmScreeningResult ParseOllamaResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return LlmScreeningResult.Error(
                "empty_response",
                "Ollama returned an empty response.");
        }

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("response", out var responseElement))
        {
            return LlmScreeningResult.Error(
                "missing_response",
                "Ollama response missing the 'response' field.",
                responseBody);
        }

        var modelText = responseElement.GetString() ?? string.Empty;
        if (!TryExtractJson(modelText, out var jsonText))
        {
            return LlmScreeningResult.Error(
                "invalid_json",
                "Model output did not contain valid JSON.",
                responseBody);
        }

        var parsed = ParseSuggestionJson(jsonText, out var errorMessage);
        if (parsed == null)
        {
            return LlmScreeningResult.Error(
                "invalid_json",
                errorMessage ?? "Unable to parse model JSON.",
                responseBody);
        }

        return LlmScreeningResult.Success(parsed, responseBody);
    }

    private static LlmScreeningSuggestion? ParseSuggestionJson(string jsonText, out string? errorMessage)
    {
        errorMessage = null;
        LlmJsonResponse? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<LlmJsonResponse>(
                jsonText,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            errorMessage = ex.Message;
            return null;
        }

        if (parsed == null || string.IsNullOrWhiteSpace(parsed.Verdict))
        {
            errorMessage = "JSON missing required 'verdict' field.";
            return null;
        }

        if (!TryMapVerdict(parsed.Verdict, out var verdict))
        {
            errorMessage = $"Unknown verdict '{parsed.Verdict}'.";
            return null;
        }

        return new LlmScreeningSuggestion
        {
            Verdict = verdict,
            Confidence = parsed.Confidence,
            Reason = parsed.Reason?.Trim(),
            RawJson = jsonText
        };
    }

    private static bool TryExtractJson(string text, out string jsonText)
    {
        jsonText = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return false;
        }

        jsonText = text[start..(end + 1)];
        return true;
    }

    private static bool TryMapVerdict(string verdictText, out ScreeningVerdict verdict)
    {
        var normalized = verdictText.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "include":
                verdict = ScreeningVerdict.Include;
                return true;
            case "exclude":
                verdict = ScreeningVerdict.Exclude;
                return true;
            case "maybe":
                verdict = ScreeningVerdict.Maybe;
                return true;
        }

        verdict = ScreeningVerdict.Pending;
        return false;
    }

    private sealed class LlmJsonResponse
    {
        public string? Verdict { get; set; }
        public double? Confidence { get; set; }
        public string? Reason { get; set; }
    }
}
