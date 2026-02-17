using FluentAssertions;
using ResearchHub.Core.Exporters;
using ResearchHub.Core.Models;
using ResearchHub.Core.Parsers;

namespace ResearchHub.Core.Tests.Exporters;

public class CsvExporterTests
{
    private readonly CsvExporter _exporter = new();

    [Fact]
    public void Export_WithAllFields_GeneratesCorrectCsv()
    {
        var references = new List<Reference>
        {
            new()
            {
                Title = "A Study on Machine Learning",
                Authors = new List<string> { "Smith, John", "Doe, Jane" },
                Abstract = "This is an abstract.",
                Journal = "Journal of AI Research",
                Year = 2024,
                Volume = "15",
                Issue = "3",
                Pages = "100-120",
                Doi = "10.1234/example.2024.001",
                Pmid = "12345678",
                Url = "http://example.com",
                Tags = new List<string> { "machine learning", "AI" }
            }
        };

        var result = _exporter.Export(references);

        result.Should().Contain("A Study on Machine Learning");
        result.Should().Contain("Smith, John; Doe, Jane");
        result.Should().Contain("This is an abstract.");
        result.Should().Contain("Journal of AI Research");
        result.Should().Contain("2024");
        result.Should().Contain("15");
        result.Should().Contain("100-120");
        result.Should().Contain("10.1234/example.2024.001");
        result.Should().Contain("12345678");
        result.Should().Contain("http://example.com");
        result.Should().Contain("machine learning; AI");
    }

    [Fact]
    public void Export_MultipleReferences_AllPresent()
    {
        var references = new List<Reference>
        {
            new() { Title = "First Article", Authors = new List<string> { "Author A" } },
            new() { Title = "Second Article", Authors = new List<string> { "Author B" } },
            new() { Title = "Third Article", Authors = new List<string> { "Author C" } }
        };

        var result = _exporter.Export(references);

        result.Should().Contain("First Article");
        result.Should().Contain("Second Article");
        result.Should().Contain("Third Article");
    }

    [Fact]
    public void Export_MinimalReference_OnlyTitle()
    {
        var references = new List<Reference>
        {
            new() { Title = "Minimal Article" }
        };

        var result = _exporter.Export(references);

        result.Should().Contain("Minimal Article");
        // Header row should still have all columns
        result.Should().Contain("Title");
        result.Should().Contain("Authors");
        result.Should().Contain("DOI");
    }

    [Fact]
    public void Export_AuthorsJoinedWithSemicolon()
    {
        var references = new List<Reference>
        {
            new()
            {
                Title = "Multi Author Study",
                Authors = new List<string> { "Alpha", "Beta", "Gamma" }
            }
        };

        var result = _exporter.Export(references);

        result.Should().Contain("Alpha; Beta; Gamma");
    }

    [Fact]
    public void Export_TagsJoinedWithSemicolon()
    {
        var references = new List<Reference>
        {
            new()
            {
                Title = "Tagged Study",
                Tags = new List<string> { "tag1", "tag2", "tag3" }
            }
        };

        var result = _exporter.Export(references);

        result.Should().Contain("tag1; tag2; tag3");
    }

    [Fact]
    public void Export_EmptyList_OnlyHeader()
    {
        var result = _exporter.Export(new List<Reference>());

        result.Should().Contain("Title");
        // Should only have the header line
        var lines = result.Trim().Split('\n');
        lines.Should().HaveCount(1);
    }

    [Fact]
    public void Export_HasCorrectHeaderColumns()
    {
        var result = _exporter.Export(new List<Reference>());

        var headerLine = result.Split('\n')[0];
        headerLine.Should().Contain("Title");
        headerLine.Should().Contain("Authors");
        headerLine.Should().Contain("Abstract");
        headerLine.Should().Contain("Journal");
        headerLine.Should().Contain("Year");
        headerLine.Should().Contain("Volume");
        headerLine.Should().Contain("Issue");
        headerLine.Should().Contain("Pages");
        headerLine.Should().Contain("DOI");
        headerLine.Should().Contain("PMID");
        headerLine.Should().Contain("URL");
        headerLine.Should().Contain("Tags");
    }

    [Fact]
    public void Roundtrip_CsvExporterToCsvParser_PreservesData()
    {
        var original = new List<Reference>
        {
            new()
            {
                Title = "Roundtrip Test Study",
                Authors = new List<string> { "Smith", "Jones" },
                Abstract = "An abstract for roundtrip",
                Journal = "Test Journal",
                Year = 2024,
                Volume = "10",
                Issue = "2",
                Pages = "50-60",
                Doi = "10.1234/roundtrip",
                Pmid = "99999",
                Url = "http://roundtrip.com"
            }
        };

        var csvContent = _exporter.Export(original);
        var parser = new CsvReferenceParser();
        var parsed = parser.Parse(csvContent).ToList();

        parsed.Should().HaveCount(1);
        var r = parsed[0];
        r.Title.Should().Be("Roundtrip Test Study");
        r.Authors.Should().BeEquivalentTo(new[] { "Smith", "Jones" });
        r.Abstract.Should().Be("An abstract for roundtrip");
        r.Journal.Should().Be("Test Journal");
        r.Year.Should().Be(2024);
        r.Volume.Should().Be("10");
        r.Issue.Should().Be("2");
        r.Pages.Should().Be("50-60");
        r.Doi.Should().Be("10.1234/roundtrip");
        r.Pmid.Should().Be("99999");
        r.Url.Should().Be("http://roundtrip.com");
    }

    [Fact]
    public void ExportToFile_CreatesFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"csv_exporter_test_{Guid.NewGuid()}.csv");
        try
        {
            var references = new List<Reference>
            {
                new() { Title = "File Export Test" }
            };

            _exporter.ExportToFile(references, tempFile);

            File.Exists(tempFile).Should().BeTrue();
            var content = File.ReadAllText(tempFile);
            content.Should().Contain("File Export Test");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Format_ReturnsCSV()
    {
        _exporter.Format.Should().Be("CSV");
    }

    [Fact]
    public void FileExtension_ReturnsCsv()
    {
        _exporter.FileExtension.Should().Be(".csv");
    }
}
