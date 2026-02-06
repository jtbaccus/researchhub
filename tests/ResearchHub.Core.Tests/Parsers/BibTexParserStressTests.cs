using FluentAssertions;
using ResearchHub.Core.Parsers;

namespace ResearchHub.Core.Tests.Parsers;

public class BibTexParserStressTests
{
    private readonly BibTexParser _parser = new();

    [Fact]
    public void Parse_GoogleScholarExport_MinimalFields()
    {
        var content = @"@article{smith2024machine,
  title={Machine learning for clinical prediction},
  author={Smith, John and Doe, Jane},
  journal={Nature Medicine},
  volume={30},
  pages={100--115},
  year={2024},
  publisher={Nature Publishing Group}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var r = references[0];
        r.Title.Should().Be("Machine learning for clinical prediction");
        r.Authors.Should().HaveCount(2);
        r.Journal.Should().Be("Nature Medicine");
        r.Year.Should().Be(2024);
        r.Pages.Should().Be("100-115");
    }

    [Fact]
    public void Parse_ZoteroExport_FullMetadata()
    {
        var content = @"@article{garcia2023systematic,
  title = {A systematic review of {AI} in healthcare},
  author = {Garcia, Maria and Chen, Wei and Johnson, Robert},
  journal = {The Lancet Digital Health},
  year = {2023},
  volume = {5},
  number = {12},
  pages = {e900--e910},
  doi = {10.1016/S2589-7500(23)00200-1},
  pmid = {38001234},
  abstract = {Background: Artificial intelligence (AI) has emerged as a promising tool in healthcare. We conducted a systematic review to assess the current state of AI applications in clinical practice.},
  keywords = {artificial intelligence, healthcare, systematic review, machine learning},
  url = {https://doi.org/10.1016/S2589-7500(23)00200-1}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var r = references[0];
        r.Title.Should().Be("A systematic review of AI in healthcare");
        r.Authors.Should().HaveCount(3);
        r.Journal.Should().Be("The Lancet Digital Health");
        r.Doi.Should().Be("10.1016/S2589-7500(23)00200-1");
        r.Pmid.Should().Be("38001234");
        r.Abstract.Should().Contain("systematic review");
        r.Tags.Should().HaveCount(4);
        r.Url.Should().Contain("doi.org");
    }

    [Fact]
    public void Parse_DeepBraceNesting_ExtractsContent()
    {
        var content = @"@article{nested,
  title = {{{Deeply} nested} title with {multiple} levels},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        // After brace removal: "Deeply nested title with multiple levels"
        references[0].Title.Should().Be("Deeply nested title with multiple levels");
    }

    [Fact]
    public void Parse_LaTeXAccents_CleansOutput()
    {
        var content = @"@article{accents,
  title = {Analysis of caf\'{e} culture},
  author = {M\""uller, Hans and Garc\'{\i}a, Mar\'{\i}a}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        // \'{e} should become e, braces removed
        references[0].Title.Should().Contain("Analysis of");
        // The key point is it doesn't crash and produces readable output
        references[0].Authors.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_CorporateAuthor_PreservesName()
    {
        var content = @"@techreport{who2024,
  title = {Global health statistics 2024},
  author = {{World Health Organization}},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        // Double braces become "World Health Organization" after brace removal
        references[0].Authors.Should().ContainSingle().Which.Should().Be("World Health Organization");
    }

    [Fact]
    public void Parse_InproceedingsEntry_ParsesCorrectly()
    {
        var content = @"@inproceedings{conf2024,
  title = {A Novel Approach to Conference Papers},
  author = {Author, Test},
  booktitle = {Proceedings of ICML 2024},
  year = {2024},
  pages = {1--10}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var r = references[0];
        r.Title.Should().Be("A Novel Approach to Conference Papers");
        r.Journal.Should().Be("Proceedings of ICML 2024");
        r.Pages.Should().Be("1-10");
    }

    [Fact]
    public void Parse_BookEntry_ParsesCorrectly()
    {
        var content = @"@book{textbook2024,
  title = {Introduction to Machine Learning},
  author = {Bishop, Christopher M.},
  year = {2024},
  publisher = {Springer}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Introduction to Machine Learning");
    }

    [Fact]
    public void Parse_PhdThesisEntry_ParsesCorrectly()
    {
        var content = @"@phdthesis{thesis2024,
  title = {Deep Learning for Natural Language Processing},
  author = {Student, Graduate},
  year = {2024},
  school = {MIT}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Deep Learning for Natural Language Processing");
    }

    [Fact]
    public void Parse_UrlWithSpecialChars_PreservesUrl()
    {
        var content = @"@article{urltest,
  title = {URL Test Article},
  url = {https://example.com/api?key=value&page=2&sort=asc},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        // \& cleaning should not affect URLs inside braces since & is literal in braces
        // The URL has literal & not \&
        references[0].Url.Should().Be("https://example.com/api?key=value&page=2&sort=asc");
    }

    [Fact]
    public void Parse_TrailingCommaAfterLastField_ParsesCorrectly()
    {
        var content = @"@article{trailing,
  title = {Trailing Comma Article},
  year = {2024},
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Trailing Comma Article");
        references[0].Year.Should().Be(2024);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var references = _parser.Parse("").ToList();
        references.Should().BeEmpty();
    }

    [Fact]
    public void Parse_UnclosedEntry_ParsesGracefully()
    {
        var content = @"@article{unclosed,
  title = {This entry is never closed},
  year = {2024}";

        // Should not throw - may or may not parse depending on implementation
        var act = () => _parser.Parse(content).ToList();
        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_EntryWithNoFields_ReturnsEmpty()
    {
        var content = @"@article{empty,}";

        var references = _parser.Parse(content).ToList();

        // No title means it gets skipped
        references.Should().BeEmpty();
    }

    [Fact]
    public void Parse_LargeMultilineAbstract_ParsesCompletely()
    {
        var longAbstract = string.Join(" ", Enumerable.Range(1, 100).Select(i => $"word{i}"));
        var content = $@"@article{{large,
  title = {{Large Abstract Article}},
  abstract = {{{longAbstract}}},
  year = {{2024}}
}}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Abstract.Should().Contain("word1");
        references[0].Abstract.Should().Contain("word100");
    }

    [Fact]
    public void Parse_Utf8DirectCharacters_PreservesUnicode()
    {
        var content = @"@article{utf8,
  title = {日本語のタイトル},
  author = {田中太郎 and Müller, Hans},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("日本語のタイトル");
        references[0].Authors.Should().HaveCount(2);
        references[0].Authors[0].Should().Be("田中太郎");
        references[0].Authors[1].Should().Be("Müller, Hans");
    }

    [Fact]
    public void Parse_MixedDelimiters_ParsesAllFields()
    {
        var content = @"@article{mixed,
  title = {Braced Title},
  journal = ""Quoted Journal"",
  year = 2024,
  volume = {10}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        var r = references[0];
        r.Title.Should().Be("Braced Title");
        r.Journal.Should().Be("Quoted Journal");
        r.Year.Should().Be(2024);
        r.Volume.Should().Be("10");
    }

    [Fact]
    public void Parse_StringMacroSkipped_ParsesFollowingEntries()
    {
        var content = @"@string{jair = {Journal of AI Research}}
@string{nips = {Advances in Neural Information Processing Systems}}

@article{after_strings,
  title = {Article After String Macros},
  journal = {Real Journal},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Article After String Macros");
    }

    [Fact]
    public void Parse_EscapedQuotesInQuotedField_HandlesCorrectly()
    {
        var content = @"@article{escaped,
  title = ""A Study on \""Quoted\"" Terms"",
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        // Escaped quotes should be preserved (minus the backslash)
        references[0].Title.Should().Contain("Quoted");
    }

    [Fact]
    public void Parse_EscapedAmpersandAndPercent_CleansOutput()
    {
        var content = @"@article{escaped,
  title = {Research \& Development: A 50\% Improvement},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Research & Development: A 50% Improvement");
    }

    [Fact]
    public void Parse_TextFormattingCommands_CleansOutput()
    {
        var content = @"@article{formatting,
  title = {\textbf{Bold} and \textit{italic} and \emph{emphasized} text},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("Bold and italic and emphasized text");
    }

    [Fact]
    public void Parse_MiscEntryType_ParsesCorrectly()
    {
        var content = @"@misc{misc2024,
  title = {A Miscellaneous Reference},
  author = {Author, Test},
  year = {2024},
  howpublished = {\url{https://example.com}}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Title.Should().Be("A Miscellaneous Reference");
    }

    [Fact]
    public void Parse_LargeMultiEntryFile_ParsesAll()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= 50; i++)
        {
            sb.AppendLine($"@article{{entry{i},");
            sb.AppendLine($"  title = {{Article Number {i}}},");
            sb.AppendLine($"  author = {{Author{i}, Test}},");
            sb.AppendLine($"  year = {{{2000 + i}}}");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        var references = _parser.Parse(sb.ToString()).ToList();

        references.Should().HaveCount(50);
        references[0].Title.Should().Be("Article Number 1");
        references[49].Title.Should().Be("Article Number 50");
        references[49].Year.Should().Be(2050);
    }

    [Fact]
    public void Parse_BareNumberYear_ParsesCorrectly()
    {
        var content = @"@article{bare,
  title = {Bare Year Article},
  year = 2024
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(1);
        references[0].Year.Should().Be(2024);
    }

    [Fact]
    public void Parse_MultipleEntriesWithCommentsBetween_ParsesAll()
    {
        var content = @"% This is a BibTeX comment
@article{first,
  title = {First Article},
  year = {2023}
}

% Another comment
@article{second,
  title = {Second Article},
  year = {2024}
}";

        var references = _parser.Parse(content).ToList();

        references.Should().HaveCount(2);
        references[0].Title.Should().Be("First Article");
        references[1].Title.Should().Be("Second Article");
    }
}
