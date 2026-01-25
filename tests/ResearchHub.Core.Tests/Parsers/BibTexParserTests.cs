using FluentAssertions;
using ResearchHub.Core.Parsers;

namespace ResearchHub.Core.Tests.Parsers;

public class BibTexParserTests
{
    private readonly BibTexParser _parser = new();

    [Fact]
    public void Parse_WithValidBibTexEntry_ReturnsReference()
    {
        var content = @"@article{smith2024ml,
  title = {A Study on Machine Learning},
  author = {Smith, John and Doe, Jane},
  journal = {Journal of AI Research},
  year = {2024},
  volume = {15},
  number = {3},
  pages = {100--120},
  doi = {10.1234/example.2024.001},
  abstract = {This is an abstract about machine learning.}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var reference = references[0];
        reference.Title.Should().Be("A Study on Machine Learning");
        reference.Authors.Should().HaveCount(2);
        reference.Authors[0].Should().Be("Smith, John");
        reference.Authors[1].Should().Be("Doe, Jane");
        reference.Abstract.Should().Be("This is an abstract about machine learning.");
        reference.Journal.Should().Be("Journal of AI Research");
        reference.Year.Should().Be(2024);
        reference.Volume.Should().Be("15");
        reference.Issue.Should().Be("3");
        reference.Pages.Should().Be("100-120");
        reference.Doi.Should().Be("10.1234/example.2024.001");
    }

    [Fact]
    public void Parse_WithMultipleEntries_ReturnsAllReferences()
    {
        var content = @"@article{first,
  title = {First Article},
  author = {Author One},
  year = {2023}
}

@article{second,
  title = {Second Article},
  author = {Author Two},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(2);
        references[0].Title.Should().Be("First Article");
        references[1].Title.Should().Be("Second Article");
    }

    [Fact]
    public void Parse_WithMissingTitle_SkipsEntry()
    {
        var content = @"@article{notitle,
  author = {Smith, John},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithKeywords_ExtractsAsTags()
    {
        var content = @"@article{keywords,
  title = {Test Article},
  keywords = {machine learning, artificial intelligence, neural networks}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Tags.Should().HaveCount(3);
        references[0].Tags.Should().Contain("machine learning");
    }

    [Fact]
    public void Parse_WithLatexCharacters_CleansOutput()
    {
        var content = @"@article{latex,
  title = {Analysis of \textit{Machine} Learning},
  author = {M\""uller, Hans}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Analysis of Machine Learning");
    }

    [Fact]
    public void Parse_WithNestedBraces_HandlesCorrectly()
    {
        var content = @"@article{nested,
  title = {{Machine Learning}: A {Comprehensive} Guide},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Machine Learning: A Comprehensive Guide");
    }

    [Fact]
    public void Parse_SkipsCommentAndStringEntries()
    {
        var content = @"@string{jair = {Journal of AI Research}}
@comment{This is a comment}
@preamble{Some preamble}
@article{real,
  title = {Real Article},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Real Article");
    }

    [Fact]
    public void Format_ReturnsBibTeX()
    {
        _parser.Format.Should().Be("BibTeX");
    }

    [Fact]
    public void SupportedExtensions_ContainsBibAndBibtex()
    {
        _parser.SupportedExtensions.Should().Contain(".bib");
        _parser.SupportedExtensions.Should().Contain(".bibtex");
    }
}
