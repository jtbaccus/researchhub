using FluentAssertions;
using ResearchHub.Core.Exporters;
using ResearchHub.Core.Models;

namespace ResearchHub.Core.Tests.Exporters;

public class RisExporterTests
{
    private readonly RisExporter _exporter = new();

    [Fact]
    public void Export_WithValidReference_GeneratesCorrectRis()
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
                Tags = new List<string> { "machine learning", "AI" }
            }
        };

        var result = _exporter.Export(references);

        result.Should().Contain("TY  - JOUR");
        result.Should().Contain("TI  - A Study on Machine Learning");
        result.Should().Contain("AU  - Smith, John");
        result.Should().Contain("AU  - Doe, Jane");
        result.Should().Contain("AB  - This is an abstract.");
        result.Should().Contain("JO  - Journal of AI Research");
        result.Should().Contain("PY  - 2024");
        result.Should().Contain("VL  - 15");
        result.Should().Contain("IS  - 3");
        result.Should().Contain("SP  - 100");
        result.Should().Contain("EP  - 120");
        result.Should().Contain("DO  - 10.1234/example.2024.001");
        result.Should().Contain("KW  - machine learning");
        result.Should().Contain("KW  - AI");
        result.Should().Contain("ER  -");
    }

    [Fact]
    public void Export_WithMultipleReferences_GeneratesAll()
    {
        var references = new List<Reference>
        {
            new() { Title = "First Article" },
            new() { Title = "Second Article" }
        };

        var result = _exporter.Export(references);

        result.Should().Contain("TI  - First Article");
        result.Should().Contain("TI  - Second Article");
        result.Split("ER  -").Length.Should().Be(3); // Two ER markers plus trailing
    }

    [Fact]
    public void Export_WithMinimalReference_OmitsEmptyFields()
    {
        var references = new List<Reference>
        {
            new() { Title = "Minimal Article" }
        };

        var result = _exporter.Export(references);

        result.Should().Contain("TI  - Minimal Article");
        result.Should().NotContain("AB  -");
        result.Should().NotContain("JO  -");
        result.Should().NotContain("PY  -");
    }

    [Fact]
    public void Format_ReturnsRIS()
    {
        _exporter.Format.Should().Be("RIS");
    }

    [Fact]
    public void FileExtension_ReturnsRis()
    {
        _exporter.FileExtension.Should().Be(".ris");
    }
}
