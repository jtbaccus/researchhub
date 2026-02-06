using FluentAssertions;
using ResearchHub.Core.Parsers;

namespace ResearchHub.Core.Tests.Parsers;

public class RisParserStressTests
{
    private readonly RisParser _parser = new();

    [Fact]
    public void Parse_PubMedExport_HandlesNonStandardDateAndAllFields()
    {
        var content = @"TY  - JOUR
TI  - Genome-wide association study of COVID-19 severity
AU  - Smith, John A.
AU  - Garcia, Maria L.
AU  - Chen, Wei
AB  - We conducted a genome-wide association study (GWAS) of COVID-19 severity.
JF  - Nature Genetics
PY  - 2024/01/15/
VL  - 56
IS  - 1
SP  - 100
EP  - 115
DO  - 10.1038/s41588-024-0001
PM  - 38123456
UR  - https://pubmed.ncbi.nlm.nih.gov/38123456/
KW  - GWAS
KW  - COVID-19
KW  - genetics
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var r = references[0];
        r.Title.Should().Be("Genome-wide association study of COVID-19 severity");
        r.Authors.Should().HaveCount(3);
        r.Authors[0].Should().Be("Smith, John A.");
        r.Year.Should().Be(2024);
        r.Journal.Should().Be("Nature Genetics");
        r.Volume.Should().Be("56");
        r.Issue.Should().Be("1");
        r.Pages.Should().Be("100-115");
        r.Doi.Should().Be("10.1038/s41588-024-0001");
        r.Pmid.Should().Be("38123456");
        r.Url.Should().Be("https://pubmed.ncbi.nlm.nih.gov/38123456/");
        r.Tags.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_ScopusExport_HandlesN1AndSNTags()
    {
        // Scopus uses N1 for notes and SN for ISSN - these aren't mapped to Reference fields
        // but the parser should still parse the entry without crashing
        var content = @"TY  - JOUR
TI  - Scopus Exported Article
AU  - Williams, Robert
JO  - Lancet Digital Health
PY  - 2023
VL  - 5
IS  - 12
SP  - e900
EP  - e910
DO  - 10.1016/S2589-7500(23)00200-1
N1  - Cited By: 42; Export Date: 1 January 2024
SN  - 2589-7500
LA  - English
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var r = references[0];
        r.Title.Should().Be("Scopus Exported Article");
        r.Journal.Should().Be("Lancet Digital Health");
        r.Pages.Should().Be("e900-e910");
    }

    [Fact]
    public void Parse_MultiLineAbstract_JoinsContinuationLines()
    {
        var content = @"TY  - JOUR
TI  - Multi-line Abstract Study
AB  - This is the first line of a very long abstract that spans
      multiple lines. The continuation lines are indented with
      spaces. This is a common pattern in RIS exports from
      databases like PubMed and Web of Science.
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var r = references[0];
        r.Abstract.Should().Contain("first line of a very long abstract");
        r.Abstract.Should().Contain("multiple lines.");
        r.Abstract.Should().Contain("PubMed and Web of Science.");
    }

    [Fact]
    public void Parse_UnicodeAuthors_PreservesAccentedCharacters()
    {
        var content = @"TY  - JOUR
TI  - International Collaboration Study
AU  - Müller, Hans
AU  - García-López, María
AU  - Ørsted, Anders
AU  - Château, François
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var r = references[0];
        r.Authors.Should().HaveCount(4);
        r.Authors[0].Should().Be("Müller, Hans");
        r.Authors[1].Should().Be("García-López, María");
        r.Authors[2].Should().Be("Ørsted, Anders");
        r.Authors[3].Should().Be("Château, François");
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var references = _parser.Parse("").ToList();
        references.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmpty()
    {
        var references = _parser.Parse("   \n\n   \r\n   ").ToList();
        references.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoErTerminator_StillParsesEntry()
    {
        var content = @"TY  - JOUR
TI  - Article Without ER Tag
AU  - Author, Test
PY  - 2024
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Article Without ER Tag");
    }

    [Fact]
    public void Parse_TagsWithEmptyValues_HandlesGracefully()
    {
        var content = @"TY  - JOUR
TI  - Article With Empty Fields
AU  -
AB  -
PY  - 2024
VL  -
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var r = references[0];
        r.Title.Should().Be("Article With Empty Fields");
        r.Authors.Should().BeEmpty();
        r.Abstract.Should().BeNull();
        r.Volume.Should().BeNull();
    }

    [Fact]
    public void Parse_MixedLineEndings_ParsesCorrectly()
    {
        // Mix \r\n and \n in the same file
        var content = "TY  - JOUR\r\nTI  - Mixed Line Endings\nAU  - Author, Test\r\nPY  - 2024\nER  -\r\n";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Mixed Line Endings");
        references[0].Authors.Should().ContainSingle().Which.Should().Be("Author, Test");
    }

    [Fact]
    public void Parse_MultipleUrls_ReturnsFirstUrl()
    {
        var content = @"TY  - JOUR
TI  - Article With Multiple URLs
UR  - https://doi.org/10.1234/example
L1  - https://example.com/fulltext.pdf
L2  - https://example.com/supplementary
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        // UR is checked first in GetFirstValue
        references[0].Url.Should().Be("https://doi.org/10.1234/example");
    }

    [Fact]
    public void Parse_PageRangeInSpTag_HandlesRange()
    {
        // Some exports put "100-120" in SP without using EP
        var content = @"TY  - JOUR
TI  - SP Range Article
SP  - 100-120
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Pages.Should().Be("100-120");
    }

    [Fact]
    public void Parse_PgTagTakesPrecedenceOverSpEp()
    {
        var content = @"TY  - JOUR
TI  - PG Priority Article
PG  - 55-60
SP  - 55
EP  - 60
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Pages.Should().Be("55-60");
    }

    [Fact]
    public void Parse_DuplicateTitleTags_UsesFirst()
    {
        var content = @"TY  - JOUR
TI  - First Title
TI  - Second Title
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("First Title");
    }

    [Fact]
    public void Parse_HtmlInAbstract_PreservesHtml()
    {
        var content = @"TY  - JOUR
TI  - Chemistry Article
AB  - The concentration of CO<sub>2</sub> was measured <i>in vivo</i> using a novel technique.
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        // Parser should preserve HTML as-is (stripping is a presentation concern)
        references[0].Abstract.Should().Contain("CO<sub>2</sub>");
        references[0].Abstract.Should().Contain("<i>in vivo</i>");
    }

    [Fact]
    public void Parse_AnTagAsPmidFallback_ExtractsPmid()
    {
        // Some databases use AN (Accession Number) for PMID
        var content = @"TY  - JOUR
TI  - AN Tag Article
AN  - 12345678
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Pmid.Should().Be("12345678");
    }

    [Fact]
    public void Parse_Y1DateFormat_ExtractsYear()
    {
        var content = @"TY  - JOUR
TI  - Y1 Date Article
Y1  - 2023/06/15
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Year.Should().Be(2023);
    }

    [Fact]
    public void Parse_DaDateFormat_ExtractsYear()
    {
        var content = @"TY  - JOUR
TI  - DA Date Article
DA  - 2022/12/01
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Year.Should().Be(2022);
    }

    [Fact]
    public void Parse_LargeMultiEntryFile_ParsesAll()
    {
        var entries = new System.Text.StringBuilder();
        for (int i = 1; i <= 50; i++)
        {
            entries.AppendLine($"TY  - JOUR");
            entries.AppendLine($"TI  - Article Number {i}");
            entries.AppendLine($"AU  - Author{i}, Test");
            entries.AppendLine($"PY  - {2000 + i}");
            entries.AppendLine($"ER  -");
            entries.AppendLine();
        }

        var references = _parser.Parse(entries.ToString()).ToList();

        references.Should().HaveCount(50);
        references[0].Title.Should().Be("Article Number 1");
        references[49].Title.Should().Be("Article Number 50");
        references[49].Year.Should().Be(2050);
    }

    [Fact]
    public void Parse_A1AuthorTag_ExtractsAuthors()
    {
        var content = @"TY  - JOUR
TI  - A1 Author Article
A1  - Smith, John
A1  - Doe, Jane
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Authors.Should().HaveCount(2);
        references[0].Authors[0].Should().Be("Smith, John");
    }

    [Fact]
    public void Parse_N2AlternateAbstract_ExtractsAbstract()
    {
        var content = @"TY  - JOUR
TI  - N2 Abstract Article
N2  - This abstract uses the N2 tag instead of AB.
PY  - 2024
ER  -
";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Abstract.Should().Be("This abstract uses the N2 tag instead of AB.");
    }
}
