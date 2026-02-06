using ResearchHub.Core.Models;

namespace ResearchHub.Services;

public interface IPrismaService
{
    Task<PrismaFlowCounts> GetFlowCountsAsync(int projectId);
}
