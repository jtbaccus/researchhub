using FluentAssertions;
using ResearchHub.Core.Parsers;

namespace ResearchHub.Core.Tests.Parsers;

public class RisParserTests
{
    private readonly RisParser _parser = new();

    [Fact]
    public void Parse_WithValidRisContent_ReturnsReferences()
    {
        var content = @"TY  - JOUR
TI  - A Study on Machine Learning
AU  - Smith, John
AU  - Doe, Jane
AB  - This is an abstract about machine learning.
JO  - Journal of AI Research
PY  - 2024
VL  - 15
IS  - 3
SP  - 100
EP  - 120
DO  - 10.1234/example.2024.001
ER  -
";

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
    public void Parse_WithMultipleReferences_ReturnsAllReferences()
    {
        var content = @"TY  - JOUR
TI  - First Article
AU  - Author One
PY  - 2023
ER  -

TY  - JOUR
TI  - Second Article
AU  - Author Two
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(2);
        references[0].Title.Should().Be("First Article");
        references[1].Title.Should().Be("Second Article");
    }

    [Fact]
    public void Parse_WithMissingTitle_SkipsReference()
    {
        var content = @"TY  - JOUR
AU  - Smith, John
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithKeywords_ExtractsAsTags()
    {
        var content = @"TY  - JOUR
TI  - Test Article
KW  - machine learning
KW  - artificial intelligence
KW  - neural networks
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Tags.Should().HaveCount(3);
        references[0].Tags.Should().Contain("machine learning");
        references[0].Tags.Should().Contain("artificial intelligence");
    }

    [Fact]
    public void Parse_WithDateFormats_ExtractsYear()
    {
        var content = @"TY  - JOUR
TI  - Article with Date
PY  - 2024/03/15
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Year.Should().Be(2024);
    }

    [Fact]
    public void Parse_WithAlternativeTitleTag_ExtractsTitle()
    {
        var content = @"TY  - JOUR
T1  - Alternative Title Tag
AU  - Author
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Alternative Title Tag");
    }

    [Fact]
    public void Parse_WithContinuationLines_AppendsToPreviousValue()
    {
        var content = @"TY  - JOUR
TI  - A Very Long Title
      That Continues On Next Line
AB  - First line of abstract
      second line of abstract
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("A Very Long Title That Continues On Next Line");
        references[0].Abstract.Should().Be("First line of abstract second line of abstract");
    }

    [Fact]
    public void Parse_WithLowercaseTags_ParsesCorrectly()
    {
        var content = @"ty  - JOUR
ti  - Lowercase Title
au  - Author One
er  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Lowercase Title");
        references[0].Authors.Should().ContainSingle().Which.Should().Be("Author One");
    }

    [Fact]
    public void Parse_WithPageRangeTag_UsesPg()
    {
        var content = @"TY  - JOUR
TI  - Page Range Article
PG  - 55-60
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Pages.Should().Be("55-60");
    }

    [Fact]
    public void Format_ReturnsRIS()
    {
        _parser.Format.Should().Be("RIS");
    }

    [Fact]
    public void SupportedExtensions_ContainsRis()
    {
        _parser.SupportedExtensions.Should().Contain(".ris");
    }
}
