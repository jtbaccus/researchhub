using ResearchHub.Core.Models;

namespace ResearchHub.Services;

public interface ILlmScreeningService
{
    Task<LlmScreeningResult> SuggestAsync(
        Reference reference,
        ScreeningPhase phase,
        string? criteria = null,
        CancellationToken cancellationToken = default);
}
