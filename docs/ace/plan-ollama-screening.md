# Plan: Local LLM Screening Assistance (Ollama)

*Date: 2026-02-06 | Project: ResearchHub*

## Goal

Add a local Ollama-backed screening assistance service that sends title/abstract to a local model, returns structured Include/Exclude/Maybe suggestions, and handles Ollama/model availability errors gracefully.

## Approach

Create a new service interface + implementation in `ResearchHub.Services` that calls Ollama’s HTTP API, with a lightweight settings model for endpoint, model name, and prompt template. The service will build a request payload, parse a strict JSON response into a suggestion model, and surface errors in a controlled result type. Wire the service in `App.axaml.cs` alongside existing services.

## Steps

1. **Define models and interface**
   - Action: Add `LlmScreeningSuggestion` and `LlmScreeningResult` models, plus `ILlmScreeningService` interface in `src/ResearchHub.Services` (or `src/ResearchHub.Core` if shared with UI).
   - Verify: Compilation succeeds and interface matches intended usage.

2. **Add settings model and defaults**
   - Action: Create `LlmScreeningSettings` with `Endpoint`, `Model`, `PromptTemplate`, and optional `TimeoutSeconds`. Provide defaults and allow environment-variable overrides.
   - Verify: Settings can be constructed without config and produce expected defaults.

3. **Implement Ollama client logic**
   - Action: Implement `LlmScreeningService` using `HttpClient` to call Ollama’s generate/chat endpoint, build prompt from settings + reference fields, and request JSON output.
   - Verify: Unit-level reasoning (no tests yet) that request body matches Ollama expectations.

4. **Robust JSON parsing and verdict mapping**
   - Action: Define a strict response schema (e.g., `{ "verdict": "include|exclude|maybe", "confidence": 0-1, "reason": "..." }`) and parse using `System.Text.Json` with validation and fallback to error result.
   - Verify: Parser returns `Error` for malformed JSON and maps known verdict strings to `ScreeningVerdict`.

5. **Error handling for Ollama availability and model missing**
   - Action: Catch connection failures, non-200 responses, and model-not-found errors; return a result with `IsSuccess=false` and a user-friendly message.
   - Verify: Service returns deterministic error messages for each failure type.

6. **Wire service into app**
   - Action: Add `ILlmScreeningService` to `App.axaml.cs` and initialize with settings + `HttpClient`.
   - Verify: App builds and service is available for future UI integration.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ResearchHub.Services/ILlmScreeningService.cs` | Create | Public interface for LLM screening suggestions |
| `src/ResearchHub.Services/LlmScreeningService.cs` | Create | Ollama HTTP client + prompt + parsing |
| `src/ResearchHub.Services/LlmScreeningModels.cs` | Create | Suggestion/result/settings models |
| `src/ResearchHub.App/App.axaml.cs` | Modify | Wire service into app initialization |

## Risks/Considerations

- Ollama API may differ by version; we’ll target the current local HTTP API and keep it configurable.
- JSON responses from models can be messy; we’ll enforce a strict schema and fail closed.
- Without UI integration, this is a backend capability only; usage can be added later.

## Success Criteria

- [ ] New service builds and is wired into the app
- [ ] Prompt template is configurable via settings
- [ ] JSON parsing returns a validated suggestion or a clear error
- [ ] Ollama-not-running and model-not-available cases return actionable error results

---

*Research artifact: `docs/ace/research-ollama-screening.md`*
