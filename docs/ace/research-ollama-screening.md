# Research: Local LLM Screening Assistance (Ollama)

*Date: 2026-02-06 | Project: ResearchHub*

## Question/Goal

Implement a local LLM screening assistance service using Ollama that can send abstracts to a local model and return Include/Exclude suggestions, following existing service patterns.

## Findings

### Key Observations

- Services live in `src/ResearchHub.Services` with simple interfaces and are manually wired in `src/ResearchHub.App/App.axaml.cs`.
- Screening decisions use `ScreeningVerdict` (`Pending`, `Include`, `Exclude`, `Maybe`) and `ScreeningPhase` (`TitleAbstract`, `FullText`) in `src/ResearchHub.Core/Models/ScreeningDecision.cs`.
- There is no existing HTTP client or config system; services are constructed directly with dependencies.
- `Reference` contains `Title` and optional `Abstract`, which are the inputs needed for LLM screening.

### Relevant Code/Files

- `src/ResearchHub.Services/ScreeningService.cs` — current screening workflow; illustrates service patterns and naming.
- `src/ResearchHub.App/App.axaml.cs` — manual service wiring; new service must be added here.
- `src/ResearchHub.Core/Models/ScreeningDecision.cs` — `ScreeningVerdict` to reuse for LLM suggestions.
- `src/ResearchHub.Core/Models/Reference.cs` — `Title`/`Abstract` fields used to build prompts.

### Constraints/Dependencies

- No existing DI container or configuration system; options must be provided via constructor or lightweight env vars.
- Should avoid DB schema changes unless required; current request only needs a service returning suggestions.
- Must follow ACE methodology and add new artifacts in `docs/ace/`.

## Conclusions

We can add a new `ILlmScreeningService`/`LlmScreeningService` in `ResearchHub.Services` that calls Ollama’s local HTTP API, builds a prompt from `Reference` title/abstract plus optional criteria, and returns a structured suggestion using `ScreeningVerdict`. The service should be wired in `App.axaml.cs` similarly to other services, with lightweight options (endpoint, model) possibly via environment variables.

---

*Ready for: Plan phase*
