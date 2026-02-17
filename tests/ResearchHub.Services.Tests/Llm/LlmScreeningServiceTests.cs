using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ResearchHub.Core.Models;

namespace ResearchHub.Services.Tests.Llm;

public class LlmScreeningServiceTests
{
    private static readonly LlmScreeningSettings DefaultSettings = new()
    {
        Endpoint = "http://localhost:11434",
        Model = "llama3",
        TimeoutSeconds = 30
    };

    private static Reference MakeRef(string title, string? @abstract = null)
    {
        return new Reference { Title = title, Abstract = @abstract };
    }

    private static LlmScreeningService CreateService(HttpMessageHandler handler, LlmScreeningSettings? settings = null)
    {
        var httpClient = new HttpClient(handler);
        return new LlmScreeningService(httpClient, settings ?? DefaultSettings);
    }

    private static FakeHttpHandler OkHandler(string ollamaResponseJson)
    {
        return new FakeHttpHandler(HttpStatusCode.OK, ollamaResponseJson);
    }

    private static string WrapOllamaResponse(string modelOutput)
    {
        return JsonSerializer.Serialize(new { response = modelOutput });
    }

    // --- Missing input ---

    [Fact]
    public async Task SuggestAsync_MissingTitleAndAbstract_ReturnsError()
    {
        var handler = OkHandler("{}");
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("", null), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("missing_input");
    }

    [Fact]
    public async Task SuggestAsync_WhitespaceTitleAndAbstract_ReturnsError()
    {
        var handler = OkHandler("{}");
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(
            new Reference { Title = "   ", Abstract = "  " },
            ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("missing_input");
    }

    [Fact]
    public async Task SuggestAsync_TitleOnly_Succeeds()
    {
        var modelOutput = "{\"verdict\":\"include\",\"confidence\":0.9,\"reason\":\"Relevant study\"}";
        var handler = OkHandler(WrapOllamaResponse(modelOutput));
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Test Study", null), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SuggestAsync_AbstractOnly_Succeeds()
    {
        var modelOutput = "{\"verdict\":\"exclude\",\"confidence\":0.8,\"reason\":\"Not relevant\"}";
        var handler = OkHandler(WrapOllamaResponse(modelOutput));
        var svc = CreateService(handler);

        var reference = new Reference { Title = "", Abstract = "This study examines..." };
        var result = await svc.SuggestAsync(reference, ScreeningPhase.TitleAbstract);

        // Title is empty string, Abstract is not - the check is for both being null/whitespace
        // Title="" is whitespace-only? Yes.
        // Both empty => error. But abstract has content, so should be fine?
        // Looking at the code: checks if BOTH are null/whitespace
        result.IsSuccess.Should().BeTrue();
    }

    // --- Successful response ---

    [Fact]
    public async Task SuggestAsync_ValidIncludeResponse_ParsedCorrectly()
    {
        var modelOutput = "{\"verdict\":\"include\",\"confidence\":0.95,\"reason\":\"Highly relevant RCT\"}";
        var handler = OkHandler(WrapOllamaResponse(modelOutput));
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("RCT Study", "Abstract about RCT"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeTrue();
        result.Suggestion.Should().NotBeNull();
        result.Suggestion!.Verdict.Should().Be(ScreeningVerdict.Include);
        result.Suggestion.Confidence.Should().BeApproximately(0.95, 0.01);
        result.Suggestion.Reason.Should().Be("Highly relevant RCT");
    }

    [Fact]
    public async Task SuggestAsync_ValidExcludeResponse_ParsedCorrectly()
    {
        var modelOutput = "{\"verdict\":\"exclude\",\"confidence\":0.7,\"reason\":\"Not an RCT\"}";
        var handler = OkHandler(WrapOllamaResponse(modelOutput));
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Observational Study", "Some abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeTrue();
        result.Suggestion!.Verdict.Should().Be(ScreeningVerdict.Exclude);
    }

    [Fact]
    public async Task SuggestAsync_ValidMaybeResponse_ParsedCorrectly()
    {
        var modelOutput = "{\"verdict\":\"maybe\",\"confidence\":0.5,\"reason\":\"Unclear methods\"}";
        var handler = OkHandler(WrapOllamaResponse(modelOutput));
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Ambiguous Study", "Unclear abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeTrue();
        result.Suggestion!.Verdict.Should().Be(ScreeningVerdict.Maybe);
    }

    [Fact]
    public async Task SuggestAsync_ResponseWithSurroundingText_ExtractsJson()
    {
        // Model sometimes outputs text around JSON
        var modelOutput = "Based on my analysis, here is my assessment:\n{\"verdict\":\"include\",\"confidence\":0.8,\"reason\":\"Relevant\"}\nHope this helps!";
        var handler = OkHandler(WrapOllamaResponse(modelOutput));
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeTrue();
        result.Suggestion!.Verdict.Should().Be(ScreeningVerdict.Include);
    }

    // --- Error responses ---

    [Fact]
    public async Task SuggestAsync_404Response_ReturnsModelNotFound()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.NotFound, "model not found");
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("model_not_found");
    }

    [Fact]
    public async Task SuggestAsync_Timeout_ReturnsTimeoutError()
    {
        var handler = new TimeoutHttpHandler();
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ollama_timeout");
    }

    [Fact]
    public async Task SuggestAsync_InvalidJsonInResponse_ReturnsInvalidJsonError()
    {
        var modelOutput = "I think this paper should be included but I can't format JSON";
        var handler = OkHandler(WrapOllamaResponse(modelOutput));
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_json");
    }

    [Fact]
    public async Task SuggestAsync_EmptyResponse_ReturnsEmptyResponseError()
    {
        var handler = OkHandler("");
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("empty_response");
    }

    [Fact]
    public async Task SuggestAsync_EmptyResponseField_ReturnsInvalidJsonError()
    {
        var handler = OkHandler(WrapOllamaResponse(""));
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_json");
    }

    [Fact]
    public async Task SuggestAsync_ServerError_ReturnsOllamaError()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, "Internal Server Error");
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ollama_error");
    }

    [Fact]
    public async Task SuggestAsync_ConnectionRefused_ReturnsUnavailable()
    {
        var handler = new ConnectionRefusedHttpHandler();
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ollama_unavailable");
    }

    // --- Prompt building ---

    [Fact]
    public async Task SuggestAsync_PromptContainsAllSubstitutions()
    {
        var capturedRequests = new List<string>();
        var modelOutput = "{\"verdict\":\"include\",\"confidence\":0.9,\"reason\":\"OK\"}";
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, WrapOllamaResponse(modelOutput), capturedRequests);

        var settings = new LlmScreeningSettings
        {
            Endpoint = "http://localhost:11434",
            Model = "test-model",
            PromptTemplate = "Phase: {phase}, Title: {title}, Abstract: {abstract}, Criteria: {criteria}",
            TimeoutSeconds = 30
        };
        var svc = CreateService(handler, settings);

        var reference = new Reference { Title = "Test Title", Abstract = "Test Abstract" };
        await svc.SuggestAsync(reference, ScreeningPhase.FullText, "Include RCTs only");

        capturedRequests.Should().HaveCount(1);
        var body = capturedRequests[0];
        body.Should().Contain("Test Title");
        body.Should().Contain("Test Abstract");
        body.Should().Contain("FullText");
        body.Should().Contain("Include RCTs only");
        body.Should().Contain("test-model");
    }

    [Fact]
    public async Task SuggestAsync_NullCriteria_UsesDefault()
    {
        var capturedRequests = new List<string>();
        var modelOutput = "{\"verdict\":\"include\",\"confidence\":0.9,\"reason\":\"OK\"}";
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, WrapOllamaResponse(modelOutput), capturedRequests);

        var settings = new LlmScreeningSettings
        {
            Endpoint = "http://localhost:11434",
            Model = "llama3",
            PromptTemplate = "Criteria: {criteria}",
            TimeoutSeconds = 30
        };
        var svc = CreateService(handler, settings);

        await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract, null);

        capturedRequests[0].Should().Contain("None provided.");
    }

    [Fact]
    public async Task SuggestAsync_NullAbstract_UsesDefault()
    {
        var capturedRequests = new List<string>();
        var modelOutput = "{\"verdict\":\"include\",\"confidence\":0.9,\"reason\":\"OK\"}";
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, WrapOllamaResponse(modelOutput), capturedRequests);

        var settings = new LlmScreeningSettings
        {
            Endpoint = "http://localhost:11434",
            Model = "llama3",
            PromptTemplate = "Abstract: {abstract}",
            TimeoutSeconds = 30
        };
        var svc = CreateService(handler, settings);

        await svc.SuggestAsync(MakeRef("Study With Title", null), ScreeningPhase.TitleAbstract);

        capturedRequests[0].Should().Contain("None provided.");
    }

    [Fact]
    public async Task SuggestAsync_EndpointUsesApiGenerate()
    {
        var capturedUrls = new List<string>();
        var modelOutput = "{\"verdict\":\"include\",\"confidence\":0.9,\"reason\":\"OK\"}";
        var handler = new UrlCapturingHttpHandler(HttpStatusCode.OK, WrapOllamaResponse(modelOutput), capturedUrls);
        var svc = CreateService(handler);

        await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        capturedUrls.Should().HaveCount(1);
        capturedUrls[0].Should().EndWith("/api/generate");
    }

    [Fact]
    public async Task SuggestAsync_EndpointWithTrailingSlash_Normalized()
    {
        var capturedUrls = new List<string>();
        var modelOutput = "{\"verdict\":\"include\",\"confidence\":0.9,\"reason\":\"OK\"}";
        var handler = new UrlCapturingHttpHandler(HttpStatusCode.OK, WrapOllamaResponse(modelOutput), capturedUrls);

        var settings = new LlmScreeningSettings
        {
            Endpoint = "http://localhost:11434/",
            Model = "llama3",
            TimeoutSeconds = 30
        };
        var svc = CreateService(handler, settings);

        await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        capturedUrls[0].Should().Be("http://localhost:11434/api/generate");
    }

    // --- Unknown verdict ---

    [Fact]
    public async Task SuggestAsync_UnknownVerdict_ReturnsInvalidJsonError()
    {
        var modelOutput = "{\"verdict\":\"unsure\",\"confidence\":0.5,\"reason\":\"Unclear\"}";
        var handler = OkHandler(WrapOllamaResponse(modelOutput));
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_json");
    }

    [Fact]
    public async Task SuggestAsync_MissingVerdictField_ReturnsInvalidJsonError()
    {
        var modelOutput = "{\"confidence\":0.5,\"reason\":\"No verdict\"}";
        var handler = OkHandler(WrapOllamaResponse(modelOutput));
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("invalid_json");
    }

    // --- Raw response preserved ---

    [Fact]
    public async Task SuggestAsync_Success_RawResponsePreserved()
    {
        var modelOutput = "{\"verdict\":\"include\",\"confidence\":0.9,\"reason\":\"Good\"}";
        var ollamaResponse = WrapOllamaResponse(modelOutput);
        var handler = OkHandler(ollamaResponse);
        var svc = CreateService(handler);

        var result = await svc.SuggestAsync(MakeRef("Study", "Abstract"), ScreeningPhase.TitleAbstract);

        result.RawResponse.Should().Be(ollamaResponse);
    }

    // --- Helper classes ---

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public FakeHttpHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private class TimeoutHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new TaskCanceledException("Request timed out");
        }
    }

    private class ConnectionRefusedHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection refused");
        }
    }

    private class CapturingHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;
        private readonly List<string> _capturedBodies;

        public CapturingHttpHandler(HttpStatusCode statusCode, string content, List<string> capturedBodies)
        {
            _statusCode = statusCode;
            _content = content;
            _capturedBodies = capturedBodies;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                _capturedBodies.Add(body);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
        }
    }

    private class UrlCapturingHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;
        private readonly List<string> _capturedUrls;

        public UrlCapturingHttpHandler(HttpStatusCode statusCode, string content, List<string> capturedUrls)
        {
            _statusCode = statusCode;
            _content = content;
            _capturedUrls = capturedUrls;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _capturedUrls.Add(request.RequestUri?.ToString() ?? "");
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            });
        }
    }
}
