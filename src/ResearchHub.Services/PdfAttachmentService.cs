using System.Security.Cryptography;
using ResearchHub.Core.Models;
using ResearchHub.Data.Repositories;

namespace ResearchHub.Services;

public class PdfAttachmentService : IPdfAttachmentService
{
    private readonly IReferenceRepository _referenceRepository;
    private readonly IReferencePdfRepository _referencePdfRepository;
    private readonly string _storageRoot;

    public PdfAttachmentService(
        IReferenceRepository referenceRepository,
        IReferencePdfRepository referencePdfRepository,
        string storageRoot)
    {
        _referenceRepository = referenceRepository;
        _referencePdfRepository = referencePdfRepository;
        _storageRoot = storageRoot;
    }

    public async Task<ReferencePdf> AddPdfAsync(int referenceId, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Source file path is required.", nameof(sourceFilePath));
        }

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("PDF file not found.", sourceFilePath);
        }

        if (!IsPdfFile(sourceFilePath))
        {
            throw new InvalidOperationException("File is not a valid PDF.");
        }

        var reference = await _referenceRepository.GetByIdAsync(referenceId);
        if (reference == null)
        {
            throw new InvalidOperationException($"Reference {referenceId} not found.");
        }

        var relativeFolder = Path.Combine("projects", reference.ProjectId.ToString(), "references", referenceId.ToString());
        var targetDirectory = Path.Combine(_storageRoot, relativeFolder);
        Directory.CreateDirectory(targetDirectory);

        var fileName = $"{Guid.NewGuid():N}.pdf";
        var destinationPath = Path.Combine(targetDirectory, fileName);
        File.Copy(sourceFilePath, destinationPath, overwrite: false);

        var fileInfo = new FileInfo(destinationPath);
        var attachment = new ReferencePdf
        {
            ReferenceId = referenceId,
            StoredPath = Path.Combine(relativeFolder, fileName),
            OriginalFileName = Path.GetFileName(sourceFilePath),
            FileSizeBytes = fileInfo.Length,
            Sha256 = await ComputeSha256Async(destinationPath),
            AddedAt = DateTime.UtcNow
        };

        await _referencePdfRepository.AddAsync(attachment);
        return attachment;
    }

    public async Task<IEnumerable<ReferencePdf>> GetPdfsAsync(int referenceId)
    {
        return await _referencePdfRepository.GetByReferenceIdAsync(referenceId);
    }

    public async Task RemovePdfAsync(int attachmentId)
    {
        var attachment = await _referencePdfRepository.GetByIdAsync(attachmentId);
        if (attachment == null)
        {
            return;
        }

        var absolutePath = GetAbsolutePath(attachment);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        await _referencePdfRepository.DeleteAsync(attachment);
    }

    public string GetAbsolutePath(ReferencePdf pdf)
    {
        return Path.Combine(_storageRoot, pdf.StoredPath);
    }

    private static bool IsPdfFile(string filePath)
    {
        if (string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[4];
            var read = stream.Read(header);
            return read == 4 && header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F';
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> ComputeSha256Async(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}
