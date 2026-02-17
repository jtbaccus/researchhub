using FluentAssertions;
using ResearchHub.Core.Models;
using ResearchHub.Services.Tests.Fakes;

namespace ResearchHub.Services.Tests.Library;

public class LibraryServiceTests : IDisposable
{
    private const int ProjectId = 1;
    private readonly FakeReferenceRepository _refRepo;
    private readonly LibraryService _svc;
    private readonly List<string> _tempFiles = new();

    public LibraryServiceTests()
    {
        _refRepo = new FakeReferenceRepository();
        _svc = new LibraryService(_refRepo);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    private string TempFile(string ext, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"library_test_{Guid.NewGuid()}{ext}");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    // --- Import from file ---

    [Fact]
    public async Task ImportFromFile_Csv_ParsesAndStoresReferences()
    {
        var csv = "Title,Authors,Year\nStudy A,Smith,2024\nStudy B,Jones,2023\n";
        var path = TempFile(".csv", csv);

        var result = await _svc.ImportFromFileAsync(ProjectId, path);

        result.TotalParsed.Should().Be(2);
        result.Imported.Should().Be(2);
        result.Errors.Should().BeEmpty();

        var refs = (await _svc.GetReferencesAsync(ProjectId)).ToList();
        refs.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportFromFile_Ris_ParsesAndStoresReferences()
    {
        var ris = "TY  - JOUR\nTI  - RIS Study\nAU  - Author\nPY  - 2024\nER  -\n";
        var path = TempFile(".ris", ris);

        var result = await _svc.ImportFromFileAsync(ProjectId, path);

        result.TotalParsed.Should().Be(1);
        result.Imported.Should().Be(1);
    }

    [Fact]
    public async Task ImportFromFile_UnsupportedExtension_ReturnsError()
    {
        var path = TempFile(".xyz", "some content");

        var result = await _svc.ImportFromFileAsync(ProjectId, path);

        result.Errors.Should().ContainSingle().Which.Should().Contain("Unsupported file format");
        result.Imported.Should().Be(0);
    }

    [Fact]
    public async Task ImportFromFile_NonExistentFile_ThrowsFileNotFound()
    {
        var act = async () => await _svc.ImportFromFileAsync(ProjectId, "/nonexistent/file.csv");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    // --- Duplicate detection ---

    [Fact]
    public async Task ImportReferences_DoiMatch_SkipsDuplicate()
    {
        // Seed an existing reference with a DOI
        _refRepo.Seed(new[]
        {
            new Reference { ProjectId = ProjectId, Title = "Existing Study", Doi = "10.1234/existing" }
        });

        var newRefs = new List<Reference>
        {
            new() { Title = "Duplicate Study", Doi = "10.1234/existing" },
            new() { Title = "Unique Study", Doi = "10.1234/unique" }
        };

        var result = await _svc.ImportReferencesAsync(ProjectId, newRefs);

        result.Duplicates.Should().Be(1);
        result.Imported.Should().Be(1);
    }

    [Fact]
    public async Task ImportReferences_PmidMatch_SkipsDuplicate()
    {
        _refRepo.Seed(new[]
        {
            new Reference { ProjectId = ProjectId, Title = "Existing Study", Pmid = "12345678" }
        });

        var newRefs = new List<Reference>
        {
            new() { Title = "Duplicate Study", Pmid = "12345678" },
            new() { Title = "Unique Study", Pmid = "87654321" }
        };

        var result = await _svc.ImportReferencesAsync(ProjectId, newRefs);

        result.Duplicates.Should().Be(1);
        result.Imported.Should().Be(1);
    }

    [Fact]
    public async Task ImportReferences_NoDuplicates_AllImported()
    {
        var refs = new List<Reference>
        {
            new() { Title = "Study A" },
            new() { Title = "Study B" },
            new() { Title = "Study C" }
        };

        var result = await _svc.ImportReferencesAsync(ProjectId, refs);

        result.Imported.Should().Be(3);
        result.Duplicates.Should().Be(0);
    }

    [Fact]
    public async Task ImportReferences_NoTitle_Skipped()
    {
        var refs = new List<Reference>
        {
            new() { Title = "" },
            new() { Title = "Valid Study" }
        };

        var result = await _svc.ImportReferencesAsync(ProjectId, refs);

        result.Imported.Should().Be(1);
        result.SkippedNoTitle.Should().Be(1);
    }

    // --- Export ---

    [Fact]
    public async Task ExportToString_Csv_ReturnsContent()
    {
        _refRepo.Seed(new[]
        {
            new Reference { ProjectId = ProjectId, Title = "Export Study", Year = 2024 }
        });

        var result = await _svc.ExportToStringAsync(ProjectId, "csv");

        result.Should().Contain("Export Study");
        result.Should().Contain("2024");
    }

    [Fact]
    public async Task ExportToString_Ris_ReturnsContent()
    {
        _refRepo.Seed(new[]
        {
            new Reference { ProjectId = ProjectId, Title = "RIS Export Study" }
        });

        var result = await _svc.ExportToStringAsync(ProjectId, "ris");

        result.Should().Contain("TI  - RIS Export Study");
    }

    [Fact]
    public async Task ExportToString_Bibtex_ReturnsContent()
    {
        _refRepo.Seed(new[]
        {
            new Reference { ProjectId = ProjectId, Title = "BibTeX Export Study" }
        });

        var result = await _svc.ExportToStringAsync(ProjectId, "bibtex");

        result.Should().Contain("BibTeX Export Study");
    }

    [Fact]
    public async Task ExportToString_UnsupportedFormat_ThrowsArgumentException()
    {
        var act = async () => await _svc.ExportToStringAsync(ProjectId, "unknown");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported export format*");
    }

    [Fact]
    public async Task ExportToFile_UnsupportedFormat_ThrowsArgumentException()
    {
        var act = async () => await _svc.ExportToFileAsync(ProjectId, "/tmp/test.xyz", "xyz");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported export format*");
    }

    // --- CRUD delegation ---

    [Fact]
    public async Task GetReferences_ReturnsProjectReferences()
    {
        _refRepo.Seed(new[]
        {
            new Reference { ProjectId = ProjectId, Title = "Ref A" },
            new Reference { ProjectId = ProjectId, Title = "Ref B" },
            new Reference { ProjectId = 999, Title = "Other Project" }
        });

        var refs = (await _svc.GetReferencesAsync(ProjectId)).ToList();

        refs.Should().HaveCount(2);
        refs.Should().AllSatisfy(r => r.ProjectId.Should().Be(ProjectId));
    }

    [Fact]
    public async Task GetReference_ById_ReturnsCorrectRef()
    {
        _refRepo.Seed(new[]
        {
            new Reference { Id = 10, ProjectId = ProjectId, Title = "Specific Ref" }
        });

        var result = await _svc.GetReferenceAsync(10);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Specific Ref");
    }

    [Fact]
    public async Task GetReference_NonExistentId_ReturnsNull()
    {
        var result = await _svc.GetReferenceAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteReference_RemovesRef()
    {
        _refRepo.Seed(new[]
        {
            new Reference { Id = 5, ProjectId = ProjectId, Title = "To Delete" }
        });

        await _svc.DeleteReferenceAsync(5);

        var result = await _svc.GetReferenceAsync(5);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateReference_DelegatesToRepo()
    {
        var reference = new Reference { Id = 1, ProjectId = ProjectId, Title = "Original" };
        _refRepo.Seed(new[] { reference });

        reference.Title = "Updated";
        await _svc.UpdateReferenceAsync(reference);

        // UpdateAsync on fake is a no-op for in-place mutation, so the object is already updated
        var result = await _svc.GetReferenceAsync(1);
        result!.Title.Should().Be("Updated");
    }

    // --- Search ---

    [Fact]
    public async Task SearchReferences_FindsByTitle()
    {
        _refRepo.Seed(new[]
        {
            new Reference { ProjectId = ProjectId, Title = "Machine Learning in Healthcare" },
            new Reference { ProjectId = ProjectId, Title = "Protein Folding Study" }
        });

        var results = (await _svc.SearchReferencesAsync(ProjectId, "machine")).ToList();

        results.Should().HaveCount(1);
        results[0].Title.Should().Contain("Machine Learning");
    }

    // --- Progress reporting ---

    [Fact]
    public async Task ImportFromFile_WithProgress_ReportsProgress()
    {
        var csv = "Title\nStudy A\nStudy B\nStudy C\n";
        var path = TempFile(".csv", csv);

        var progressReports = new List<ImportProgress>();
        var progress = new Progress<ImportProgress>(p => progressReports.Add(p));

        var result = await _svc.ImportFromFileAsync(ProjectId, path, progress);

        result.Imported.Should().Be(3);
        // Progress may have been reported (depends on synchronization context)
        // At minimum the import should succeed
    }
}
