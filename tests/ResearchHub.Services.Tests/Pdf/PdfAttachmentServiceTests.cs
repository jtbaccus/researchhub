using FluentAssertions;
using ResearchHub.Core.Models;
using ResearchHub.Services.Tests.Fakes;

namespace ResearchHub.Services.Tests.Pdf;

public class PdfAttachmentServiceTests : IDisposable
{
    private const int ProjectId = 1;
    private readonly FakeReferenceRepository _refRepo;
    private readonly FakeReferencePdfRepository _pdfRepo;
    private readonly string _storageRoot;
    private readonly PdfAttachmentService _svc;
    private readonly List<string> _tempFiles = new();

    public PdfAttachmentServiceTests()
    {
        _refRepo = new FakeReferenceRepository();
        _pdfRepo = new FakeReferencePdfRepository();
        _storageRoot = Path.Combine(Path.GetTempPath(), $"researchhub_pdf_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_storageRoot);
        _svc = new PdfAttachmentService(_refRepo, _pdfRepo, _storageRoot);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
        try
        {
            if (Directory.Exists(_storageRoot))
                Directory.Delete(_storageRoot, recursive: true);
        }
        catch { }
    }

    private void SeedReference(int id)
    {
        _refRepo.Seed(new[] { new Reference { Id = id, ProjectId = ProjectId, Title = $"Study {id}" } });
    }

    private string CreateTempPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.pdf");
        // Write a minimal PDF header so the service recognizes it
        File.WriteAllBytes(path, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }); // %PDF-1.4
        _tempFiles.Add(path);
        return path;
    }

    private string CreateTempNonPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "This is not a PDF file.");
        _tempFiles.Add(path);
        return path;
    }

    // --- AddPdf ---

    [Fact]
    public async Task AddPdf_CopiesFileAndStoresMetadata()
    {
        SeedReference(1);
        var pdfPath = CreateTempPdf();

        var result = await _svc.AddPdfAsync(1, pdfPath);

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.ReferenceId.Should().Be(1);
        result.OriginalFileName.Should().Be(Path.GetFileName(pdfPath));
        result.StoredPath.Should().NotBeNullOrEmpty();
        result.Sha256.Should().NotBeNullOrEmpty();
        result.FileSizeBytes.Should().BeGreaterThan(0);

        // File should actually exist in storage
        var storedAbsPath = _svc.GetAbsolutePath(result);
        File.Exists(storedAbsPath).Should().BeTrue();
    }

    [Fact]
    public async Task AddPdf_ComputesSha256()
    {
        SeedReference(1);
        var pdfPath = CreateTempPdf();

        var result = await _svc.AddPdfAsync(1, pdfPath);

        result.Sha256.Should().NotBeNullOrEmpty();
        // SHA256 is 64 hex chars
        result.Sha256!.Length.Should().Be(64);
        result.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task AddPdf_MissingFile_ThrowsFileNotFoundException()
    {
        SeedReference(1);

        var act = async () => await _svc.AddPdfAsync(1, "/nonexistent/file.pdf");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task AddPdf_NonPdfFile_ThrowsInvalidOperationException()
    {
        SeedReference(1);
        var txtPath = CreateTempNonPdf();

        var act = async () => await _svc.AddPdfAsync(1, txtPath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a valid PDF*");
    }

    [Fact]
    public async Task AddPdf_InvalidReferenceId_ThrowsInvalidOperationException()
    {
        var pdfPath = CreateTempPdf();

        var act = async () => await _svc.AddPdfAsync(999, pdfPath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Reference 999 not found*");
    }

    [Fact]
    public async Task AddPdf_EmptySourcePath_ThrowsArgumentException()
    {
        SeedReference(1);

        var act = async () => await _svc.AddPdfAsync(1, "");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddPdf_WhitespaceSourcePath_ThrowsArgumentException()
    {
        SeedReference(1);

        var act = async () => await _svc.AddPdfAsync(1, "   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // --- GetPdfs ---

    [Fact]
    public async Task GetPdfs_ReturnsAttachmentsForReference()
    {
        SeedReference(1);
        SeedReference(2);

        var pdf1 = CreateTempPdf();
        var pdf2 = CreateTempPdf();
        var pdf3 = CreateTempPdf();

        await _svc.AddPdfAsync(1, pdf1);
        await _svc.AddPdfAsync(1, pdf2);
        await _svc.AddPdfAsync(2, pdf3);

        var ref1Pdfs = (await _svc.GetPdfsAsync(1)).ToList();
        var ref2Pdfs = (await _svc.GetPdfsAsync(2)).ToList();

        ref1Pdfs.Should().HaveCount(2);
        ref2Pdfs.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPdfs_NoAttachments_ReturnsEmpty()
    {
        var pdfs = (await _svc.GetPdfsAsync(1)).ToList();
        pdfs.Should().BeEmpty();
    }

    // --- RemovePdf ---

    [Fact]
    public async Task RemovePdf_DeletesFileAndRecord()
    {
        SeedReference(1);
        var pdfPath = CreateTempPdf();

        var attachment = await _svc.AddPdfAsync(1, pdfPath);
        var storedPath = _svc.GetAbsolutePath(attachment);
        File.Exists(storedPath).Should().BeTrue();

        await _svc.RemovePdfAsync(attachment.Id);

        File.Exists(storedPath).Should().BeFalse();
        var remaining = (await _svc.GetPdfsAsync(1)).ToList();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task RemovePdf_NonExistentId_DoesNotThrow()
    {
        var act = async () => await _svc.RemovePdfAsync(999);

        await act.Should().NotThrowAsync();
    }

    // --- GetAbsolutePath ---

    [Fact]
    public void GetAbsolutePath_CombinesStorageRootAndRelativePath()
    {
        var pdf = new ReferencePdf
        {
            StoredPath = "projects/1/references/1/test.pdf"
        };

        var result = _svc.GetAbsolutePath(pdf);

        result.Should().Be(Path.Combine(_storageRoot, "projects/1/references/1/test.pdf"));
    }

    // --- Multiple attachments per reference ---

    [Fact]
    public async Task AddPdf_MultiplePdfsToSameReference_AllStored()
    {
        SeedReference(1);

        var pdf1 = CreateTempPdf();
        var pdf2 = CreateTempPdf();

        var attachment1 = await _svc.AddPdfAsync(1, pdf1);
        var attachment2 = await _svc.AddPdfAsync(1, pdf2);

        attachment1.Id.Should().NotBe(attachment2.Id);

        var pdfs = (await _svc.GetPdfsAsync(1)).ToList();
        pdfs.Should().HaveCount(2);

        // Both files should exist
        File.Exists(_svc.GetAbsolutePath(attachment1)).Should().BeTrue();
        File.Exists(_svc.GetAbsolutePath(attachment2)).Should().BeTrue();
    }
}
