using FluentAssertions;
using ResearchHub.Core.Parsers;

namespace ResearchHub.Core.Tests.Parsers;

public class CsvReferenceParserTests
{
    private readonly CsvReferenceParser _parser = new();

    // --- Column mapping ---

    [Fact]
    public void Parse_WithStandardHeaders_MapsAllFields()
    {
        var csv = "Title,Authors,Abstract,Journal,Year,Volume,Issue,Pages,DOI,PMID,URL\n" +
                  "Test Study,Smith; Jones,An abstract,Nature,2024,10,3,100-110,10.1234/test,12345678,http://example.com\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        var r = refs[0];
        r.Title.Should().Be("Test Study");
        r.Authors.Should().BeEquivalentTo(new[] { "Smith", "Jones" });
        r.Abstract.Should().Be("An abstract");
        r.Journal.Should().Be("Nature");
        r.Year.Should().Be(2024);
        r.Volume.Should().Be("10");
        r.Issue.Should().Be("3");
        r.Pages.Should().Be("100-110");
        r.Doi.Should().Be("10.1234/test");
        r.Pmid.Should().Be("12345678");
        r.Url.Should().Be("http://example.com");
    }

    [Fact]
    public void Parse_WithAliasHeaders_MapsCorrectly()
    {
        var csv = "paper_title,author_names,summary,source,pub_year,vol,issue_number,page_range,digital_object_identifier,pubmed_id,web_link\n" +
                  "Alias Study,Author A; Author B,A summary,Science,2023,5,2,50-60,10.5678/alias,87654321,http://alias.com\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        var r = refs[0];
        r.Title.Should().Be("Alias Study");
        r.Authors.Should().BeEquivalentTo(new[] { "Author A", "Author B" });
        r.Abstract.Should().Be("A summary");
        r.Journal.Should().Be("Science");
        r.Year.Should().Be(2023);
        r.Volume.Should().Be("5");
        r.Issue.Should().Be("2");
        r.Pages.Should().Be("50-60");
        r.Doi.Should().Be("10.5678/alias");
        r.Pmid.Should().Be("87654321");
        r.Url.Should().Be("http://alias.com");
    }

    [Fact]
    public void Parse_WithMixedCaseHeaders_MapsCorrectly()
    {
        var csv = "TITLE,AuThOrS,abstract,JOURNAL,Year\n" +
                  "Mixed Case Study,Author One,Some abstract,BMJ,2022\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        refs[0].Title.Should().Be("Mixed Case Study");
        refs[0].Authors.Should().ContainSingle().Which.Should().Be("Author One");
        refs[0].Abstract.Should().Be("Some abstract");
        refs[0].Journal.Should().Be("BMJ");
        refs[0].Year.Should().Be(2022);
    }

    // --- Author splitting ---

    [Fact]
    public void Parse_AuthorsSplitBySemicolon()
    {
        var csv = "Title,Authors\n" +
                  "Study,Smith J;  Jones M ; Brown K\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        refs[0].Authors.Should().BeEquivalentTo(new[] { "Smith J", "Jones M", "Brown K" });
    }

    [Fact]
    public void Parse_AuthorsSplitByPipe()
    {
        var csv = "Title,Authors\n" +
                  "Study,Smith J|Jones M|Brown K\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        refs[0].Authors.Should().BeEquivalentTo(new[] { "Smith J", "Jones M", "Brown K" });
    }

    [Fact]
    public void Parse_EmptyAuthors_ReturnsEmptyList()
    {
        var csv = "Title,Authors\n" +
                  "Study,\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        refs[0].Authors.Should().BeEmpty();
    }

    // --- Year extraction ---

    [Fact]
    public void Parse_Year_PlainFourDigits()
    {
        var csv = "Title,Year\nStudy,2024\n";
        var refs = _parser.Parse(csv).ToList();
        refs[0].Year.Should().Be(2024);
    }

    [Fact]
    public void Parse_Year_DateFormat()
    {
        var csv = "Title,Year\nStudy,2024/03/15\n";
        var refs = _parser.Parse(csv).ToList();
        refs[0].Year.Should().Be(2024);
    }

    [Fact]
    public void Parse_Year_MonthYear()
    {
        var csv = "Title,Year\nStudy,March 2024\n";
        var refs = _parser.Parse(csv).ToList();
        refs[0].Year.Should().Be(2024);
    }

    [Fact]
    public void Parse_Year_Invalid_ReturnsNull()
    {
        var csv = "Title,Year\nStudy,not-a-year\n";
        var refs = _parser.Parse(csv).ToList();
        refs[0].Year.Should().BeNull();
    }

    [Fact]
    public void Parse_Year_Empty_ReturnsNull()
    {
        var csv = "Title,Year\nStudy,\n";
        var refs = _parser.Parse(csv).ToList();
        refs[0].Year.Should().BeNull();
    }

    // --- Custom fields ---

    [Fact]
    public void Parse_UnmappedColumns_StoredInCustomFields()
    {
        var csv = "Title,Custom1,Custom2\n" +
                  "Study,value1,value2\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        refs[0].CustomFields.Should().ContainKey("Custom1").WhoseValue.Should().Be("value1");
        refs[0].CustomFields.Should().ContainKey("Custom2").WhoseValue.Should().Be("value2");
    }

    [Fact]
    public void Parse_UnmappedColumnsWithEmptyValues_NotStoredInCustomFields()
    {
        var csv = "Title,Custom1\n" +
                  "Study,\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        refs[0].CustomFields.Should().NotContainKey("Custom1");
    }

    // --- Edge cases ---

    [Fact]
    public void Parse_EmptyContent_Throws()
    {
        // CsvHelper throws when there is no header to read
        var act = () => _parser.Parse("").ToList();
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_HeadersOnly_ReturnsEmptyList()
    {
        var csv = "Title,Authors,Year\n";
        var refs = _parser.Parse(csv).ToList();
        refs.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MissingTitle_RowSkipped()
    {
        var csv = "Title,Authors,Year\n" +
                  ",Smith,2024\n" +
                  "Valid Study,Jones,2023\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        refs[0].Title.Should().Be("Valid Study");
    }

    [Fact]
    public void Parse_SpecialCharactersInQuotedFields()
    {
        var csv = "Title,Authors,Abstract\n" +
                  "\"Study with \"\"quotes\"\"\",\"Smith, Jr.\",\"Abstract with, commas\"\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(1);
        refs[0].Title.Should().Contain("quotes");
        refs[0].Abstract.Should().Contain("commas");
    }

    [Fact]
    public void Parse_MultipleRows_ReturnsAll()
    {
        var csv = "Title,Year\n" +
                  "Study One,2022\n" +
                  "Study Two,2023\n" +
                  "Study Three,2024\n";

        var refs = _parser.Parse(csv).ToList();

        refs.Should().HaveCount(3);
        refs[0].Title.Should().Be("Study One");
        refs[1].Title.Should().Be("Study Two");
        refs[2].Title.Should().Be("Study Three");
    }

    // --- Format metadata ---

    [Fact]
    public void Format_ReturnsCSV()
    {
        _parser.Format.Should().Be("CSV");
    }

    [Fact]
    public void SupportedExtensions_ContainsCsv()
    {
        _parser.SupportedExtensions.Should().Contain(".csv");
    }

    // --- ParseFile ---

    [Fact]
    public void ParseFile_SetsSourceFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"csv_parser_test_{Guid.NewGuid()}.csv");
        try
        {
            File.WriteAllText(tempFile, "Title\nTest Study\n");
            var refs = _parser.ParseFile(tempFile).ToList();

            refs.Should().HaveCount(1);
            refs[0].SourceFile.Should().Be(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
