using FluentAssertions;
using ResearchHub.Core.Exporters;
using ResearchHub.Core.Models;

namespace ResearchHub.Core.Tests.Exporters;

public class BibTexExporterTests
{
    private readonly BibTexExporter _exporter = new();

    [Fact]
    public void Export_WithValidReference_GeneratesCorrectBibTeX()
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

        result.Should().Contain("@article{");
        result.Should().Contain("title = {A Study on Machine Learning}");
        result.Should().Contain("author = {Smith, John and Doe, Jane}");
        result.Should().Contain("abstract = {This is an abstract.}");
        result.Should().Contain("journal = {Journal of AI Research}");
        result.Should().Contain("year = {2024}");
        result.Should().Contain("volume = {15}");
        result.Should().Contain("number = {3}");
        result.Should().Contain("pages = {100--120}");
        result.Should().Contain("doi = {10.1234/example.2024.001}");
        result.Should().Contain("keywords = {machine learning, AI}");
    }

    [Fact]
    public void Export_GeneratesUniqueCiteKeys()
    {
        var references = new List<Reference>
        {
            new() { Title = "Study One", Authors = new List<string> { "Smith" }, Year = 2024 },
            new() { Title = "Study Two", Authors = new List<string> { "Smith" }, Year = 2024 }
        };

        var result = _exporter.Export(references);

        // Should have two different cite keys (second one gets 'b' suffix since first takes no suffix)
        result.Should().Contain("@article{smith2024study,");
        result.Should().Contain("@article{smith2024studyb,");
    }

    [Fact]
    public void Export_WithSpecialCharacters_EscapesThem()
    {
        var references = new List<Reference>
        {
            new()
            {
                Title = "Analysis of 50% performance & other metrics",
                Authors = new List<string> { "Smith" }
            }
        };

        var result = _exporter.Export(references);

        result.Should().Contain(@"\%");
        result.Should().Contain(@"\&");
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

        result.Should().Contain("title = {First Article}");
        result.Should().Contain("title = {Second Article}");
        result.Split("@article{").Length.Should().Be(3); // Two entries plus leading
    }

    [Fact]
    public void Format_ReturnsBibTeX()
    {
        _exporter.Format.Should().Be("BibTeX");
    }

    [Fact]
    public void FileExtension_ReturnsBib()
    {
        _exporter.FileExtension.Should().Be(".bib");
    }
}
