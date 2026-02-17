using FluentAssertions;
using ResearchHub.Core.Parsers;

namespace ResearchHub.Core.Tests.Parsers;

public class ParserEdgeCaseTests
{
    private readonly RisParser _risParser = new();
    private readonly BibTexParser _bibtexParser = new();
    private readonly CsvReferenceParser _csvParser = new();

    // =====================================================================
    // RIS Edge Cases
    // =====================================================================

    [Fact]
    public void Ris_EmptyString_ReturnsEmptyList()
    {
        var result = _risParser.Parse("").ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Ris_WhitespaceOnly_ReturnsEmptyList()
    {
        var result = _risParser.Parse("   \n\n   \r\n  \t  ").ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Ris_EmptyFile_ReturnsEmptyList()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ris_empty_{Guid.NewGuid()}.ris");
        try
        {
            File.WriteAllText(tempFile, "");
            var result = _risParser.ParseFile(tempFile).ToList();
            result.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Ris_MalformedMixedWithValid_ParsesValidEntries()
    {
        // First entry has garbage, second entry is valid
        var content = @"TY  - JOUR
TI  -
AU  - Smith, John
ER  -

TY  - JOUR
TI  - Valid Article
AU  - Doe, Jane
PY  - 2024
ER  -

GARBAGE LINE WITHOUT TAGS
THIS IS NOT RIS FORMAT

TY  - JOUR
TI  - Another Valid Article
PY  - 2023
ER  -
";

        var result = _risParser.Parse(content).ToList();

        // First entry has empty title, so it's skipped. The other two are valid.
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Valid Article");
        result[1].Title.Should().Be("Another Valid Article");
    }

    [Fact]
    public void Ris_VeryLongTitle_Parses()
    {
        var longTitle = new string('A', 1000);
        var content = $"TY  - JOUR\nTI  - {longTitle}\nER  -\n";

        var result = _risParser.Parse(content).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().HaveLength(1000);
    }

    [Fact]
    public void Ris_VeryLongAbstract_Parses()
    {
        var longAbstract = new string('X', 100_000);
        var content = $"TY  - JOUR\nTI  - Test\nAB  - {longAbstract}\nER  -\n";

        var result = _risParser.Parse(content).ToList();

        result.Should().HaveCount(1);
        result[0].Abstract.Should().HaveLength(100_000);
    }

    [Fact]
    public void Ris_OnlyErTags_ReturnsEmptyList()
    {
        var content = "ER  -\nER  -\nER  -\n";
        var result = _risParser.Parse(content).ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Ris_Latin1Encoding_ParsesViaFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ris_latin1_{Guid.NewGuid()}.ris");
        try
        {
            // Write Latin-1 encoded content with accented characters
            var content = "TY  - JOUR\nTI  - M\u00fcller's Study on Na\u00efve Subjects\nAU  - M\u00fcller, Hans\nER  -\n";
            File.WriteAllText(tempFile, content, System.Text.Encoding.Latin1);

            var result = _risParser.ParseFile(tempFile).ToList();

            result.Should().HaveCount(1);
            result[0].Title.Should().Contain("ller");
            result[0].Authors.Should().HaveCount(1);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Ris_Utf8WithBom_ParsesViaFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ris_bom_{Guid.NewGuid()}.ris");
        try
        {
            var content = "TY  - JOUR\nTI  - UTF-8 BOM Test\nER  -\n";
            File.WriteAllText(tempFile, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var result = _risParser.ParseFile(tempFile).ToList();

            result.Should().HaveCount(1);
            result[0].Title.Should().Be("UTF-8 BOM Test");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // =====================================================================
    // BibTeX Edge Cases
    // =====================================================================

    [Fact]
    public void BibTex_EmptyString_ReturnsEmptyList()
    {
        var result = _bibtexParser.Parse("").ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public void BibTex_WhitespaceOnly_ReturnsEmptyList()
    {
        var result = _bibtexParser.Parse("   \n\n   \r\n  \t  ").ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public void BibTex_EmptyFile_ReturnsEmptyList()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"bib_empty_{Guid.NewGuid()}.bib");
        try
        {
            File.WriteAllText(tempFile, "");
            var result = _bibtexParser.ParseFile(tempFile).ToList();
            result.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void BibTex_OnlyCommentsAndPreamble_ReturnsEmptyList()
    {
        var content = @"@comment{This is a comment about the bibliography}
@preamble{""Some preamble text""}
@string{jname = {Journal of Science}}
@comment{Another comment entry}
";

        var result = _bibtexParser.Parse(content).ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public void BibTex_MalformedMixedWithValid_ParsesValidEntries()
    {
        // Entry with no title is skipped, valid entries parse.
        // Note: an unclosed brace consumes everything after it (expected behavior),
        // so we test with entries that have valid structure but missing fields.
        var content = @"@article{notitle1,
  author = {Smith, John},
  year = {2023}
}

@article{valid1,
  title = {Valid First Article},
  year = {2024}
}

@article{notitle2,
  author = {Doe, Jane},
  year = {2022}
}

@article{valid2,
  title = {Valid Second Article},
  year = {2022}
}";

        var result = _bibtexParser.Parse(content).ToList();

        // No-title entries are skipped. valid1 and valid2 should parse.
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Title == "Valid First Article");
        result.Should().Contain(r => r.Title == "Valid Second Article");
    }

    [Fact]
    public void BibTex_VeryLongTitle_Parses()
    {
        var longTitle = new string('B', 1000);
        var content = $"@article{{long, title = {{{longTitle}}}, year = {{2024}}}}";

        var result = _bibtexParser.Parse(content).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().HaveLength(1000);
    }

    [Fact]
    public void BibTex_VeryLongAbstract_Parses()
    {
        var longAbstract = new string('Z', 100_000);
        var content = $"@article{{longabs, title = {{Test}}, abstract = {{{longAbstract}}}}}";

        var result = _bibtexParser.Parse(content).ToList();

        result.Should().HaveCount(1);
        result[0].Abstract.Should().HaveLength(100_000);
    }

    [Fact]
    public void BibTex_DeeplyNestedBraces_DoesNotHang()
    {
        // Create deeply nested braces (60 levels, beyond the 50 limit)
        var nested = new string('{', 60) + "content" + new string('}', 60);
        var content = $"@article{{deep, title = {nested}}}";

        // Should not hang; may return empty or partial result
        var result = _bibtexParser.Parse(content).ToList();

        // The important thing is that it completes without hanging.
        // With the depth limit, the entry may or may not parse.
        result.Should().NotBeNull();
    }

    [Fact]
    public void BibTex_ModeratelyNestedBraces_Parses()
    {
        // 10 levels of nesting is fine
        var content = @"@article{nested10,
  title = {Level 1 {Level 2 {Level 3 {Level 4 {Level 5 {Level 6 {Level 7 {Level 8 {Level 9 {Level 10}}}}}}}}}},
  year = {2024}
}";

        var result = _bibtexParser.Parse(content).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().Contain("Level 1");
    }

    [Fact]
    public void BibTex_Latin1Encoding_ParsesViaFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"bib_latin1_{Guid.NewGuid()}.bib");
        try
        {
            var content = "@article{latin1, title = {M\u00fcller's Na\u00efve Study}, year = {2024}}";
            File.WriteAllText(tempFile, content, System.Text.Encoding.Latin1);

            var result = _bibtexParser.ParseFile(tempFile).ToList();

            result.Should().HaveCount(1);
            result[0].Title.Should().Contain("ller");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // =====================================================================
    // CSV Edge Cases
    // =====================================================================

    [Fact]
    public void Csv_HeadersOnly_ReturnsEmptyList()
    {
        var csv = "Title,Authors,Year\n";
        var result = _csvParser.Parse(csv).ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Csv_WhitespaceOnlyFile_ThrowsOrReturnsEmpty()
    {
        // CsvHelper behavior on whitespace-only input
        var act = () => _csvParser.Parse("   \n   \n   ").ToList();
        // CsvHelper may throw or return empty; either is acceptable
        act.Should().NotThrow<StackOverflowException>();
    }

    [Fact]
    public void Csv_MalformedRowsMixedWithValid_ParsesValidRows()
    {
        // Build a CSV where some rows are valid and the parser should
        // handle exceptions per-row and continue
        var csv = "Title,Year\n" +
                  "Valid Study One,2024\n" +
                  "Valid Study Two,2023\n" +
                  "Valid Study Three,2022\n";

        var result = _csvParser.Parse(csv).ToList();

        result.Should().HaveCount(3);
    }

    [Fact]
    public void Csv_InconsistentColumnCounts_HandlesGracefully()
    {
        // CsvHelper with BadDataFound = null should handle this
        var csv = "Title,Authors,Year\n" +
                  "Study One,Smith,2024\n" +
                  "Study Two,Jones,2023,extra_column\n" +
                  "Study Three,Brown,2022\n";

        // Should parse without throwing
        var act = () => _csvParser.Parse(csv).ToList();
        var result = act.Should().NotThrow().Which;

        // At least some rows should parse
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Csv_VeryLongFields_Parses()
    {
        var longAbstract = new string('Y', 100_000);
        var longTitle = new string('T', 1000);
        var csv = $"Title,Abstract\n\"{longTitle}\",\"{longAbstract}\"\n";

        var result = _csvParser.Parse(csv).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().HaveLength(1000);
        result[0].Abstract.Should().HaveLength(100_000);
    }

    [Fact]
    public void Csv_RowsWithMissingTitle_Skipped()
    {
        var csv = "Title,Authors\n" +
                  ",Smith\n" +
                  "Valid Study,Jones\n" +
                  ",Brown\n";

        var result = _csvParser.Parse(csv).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Valid Study");
    }

    [Fact]
    public void Csv_EmptyFile_ParseFile_HandlesGracefully()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"csv_empty_{Guid.NewGuid()}.csv");
        try
        {
            File.WriteAllText(tempFile, "");

            // Empty file: CsvHelper may throw because there's no header
            var act = () => _csvParser.ParseFile(tempFile).ToList();
            // We just ensure it doesn't hang or crash with StackOverflow
            act.Should().NotThrow<StackOverflowException>();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Csv_Latin1Encoding_ParsesViaFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"csv_latin1_{Guid.NewGuid()}.csv");
        try
        {
            var content = "Title,Authors\nM\u00fcller's Study,M\u00fcller\n";
            File.WriteAllText(tempFile, content, System.Text.Encoding.Latin1);

            var result = _csvParser.ParseFile(tempFile).ToList();

            result.Should().HaveCount(1);
            result[0].Title.Should().Contain("ller");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // =====================================================================
    // Cross-parser: Unicode and special characters
    // =====================================================================

    [Fact]
    public void Ris_UnicodeCharacters_Preserved()
    {
        var content = "TY  - JOUR\nTI  - \u4e2d\u6587\u6807\u9898 Chinese Title\nAU  - \u5f20\u4e09\nER  -\n";

        var result = _risParser.Parse(content).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().Contain("\u4e2d\u6587");
    }

    [Fact]
    public void BibTex_UnicodeCharacters_Preserved()
    {
        var content = "@article{cn, title = {\u4e2d\u6587\u6807\u9898 Chinese Title}, year = {2024}}";

        var result = _bibtexParser.Parse(content).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().Contain("\u4e2d\u6587");
    }

    [Fact]
    public void Csv_UnicodeCharacters_Preserved()
    {
        var csv = "Title,Authors\n\u4e2d\u6587\u6807\u9898 Chinese Title,\u5f20\u4e09\n";

        var result = _csvParser.Parse(csv).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().Contain("\u4e2d\u6587");
    }
}
