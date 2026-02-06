using ResearchHub.Core.Models;

namespace ResearchHub.Services;

public interface IPdfAttachmentService
{
    Task<ReferencePdf> AddPdfAsync(int referenceId, string sourceFilePath);
    Task<IEnumerable<ReferencePdf>> GetPdfsAsync(int referenceId);
    Task RemovePdfAsync(int attachmentId);
    string GetAbsolutePath(ReferencePdf pdf);
}
