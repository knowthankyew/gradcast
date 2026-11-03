using GradCast.Api.Services;
using Xunit;

namespace GradCast.Api.Tests;

public class SearchQueryParserTests
{
    [Fact]
    public void Parse_ExtractsExactQuotedPhrasesAndUnquotedFuzzyTokens()
    {
        var parsed = SearchQueryParser.Parse("  \"University   of Texas\"  Austin, TX —  ");

        Assert.False(parsed.IsEmpty);
        Assert.Single(parsed.ExactPhrases);
        Assert.Equal("University of Texas", parsed.ExactPhrases[0]);
        Assert.Equal(["Austin", "TX"], parsed.FuzzyTerms);
    }

    [Fact]
    public void Parse_NormalizesSmartQuotesAndTreatsUnclosedTrailingQuoteAsFuzzyPreview()
    {
        var smartQuoted = SearchQueryParser.Parse("\u201CTexas College\u201D");
        Assert.Single(smartQuoted.ExactPhrases);
        Assert.Equal("Texas College", smartQuoted.ExactPhrases[0]);
        Assert.Empty(smartQuoted.FuzzyTerms);
        Assert.Equal("Texas College", smartQuoted.NormalizedFullText);

        var midTyping = SearchQueryParser.Parse("\"Tex Aust");
        Assert.Empty(midTyping.ExactPhrases);
        Assert.Equal(["Tex", "Aust"], midTyping.FuzzyTerms);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("\"\"", 0)]
    [InlineData("\"   \"", 0)]
    [InlineData(", — ;", 0)]
    [InlineData("\"a\"", 1)]
    [InlineData("\"a", 1)]
    [InlineData("\"UT\"", 2)]
    [InlineData("A M", 2)]
    [InlineData("A&M", 3)]
    public void GetEffectiveLength_CountsOnlySearchableCharacters(string? input, int expectedLength)
    {
        Assert.Equal(expectedLength, SearchQueryParser.GetEffectiveLength(input));
    }

    [Fact]
    public void Matches_DistinguishesUnanchoredSubstringFromExactQuotedWordBoundaries()
    {
        var unquotedArt = SearchQueryParser.Parse("art");
        Assert.True(unquotedArt.Matches("University of Massachusetts-Dartmouth", "North Dartmouth", "MA"));
        Assert.True(unquotedArt.Matches("Hartnell College", "Salinas", "CA"));
        Assert.True(unquotedArt.Matches("Academy of Art University", "San Francisco", "CA"));

        var quotedArt = SearchQueryParser.Parse("\"art\"");
        Assert.True(quotedArt.Matches("Academy of Art University", "San Francisco", "CA"));
        Assert.False(quotedArt.Matches("University of Massachusetts-Dartmouth", "North Dartmouth", "MA"));
        Assert.False(quotedArt.Matches("Hartnell College", "Salinas", "CA"));
        Assert.False(quotedArt.Matches("University of the Arts", "Philadelphia", "PA"));

        var quotedHyphenMetro = SearchQueryParser.Parse("\"Round Rock\"");
        Assert.True(quotedHyphenMetro.Matches("Austin-Round Rock-Georgetown", "TX"));
    }

    [Fact]
    public void ToUnanchoredLikePattern_EscapesWildcardsAndBackslash()
    {
        var pattern = SearchQueryParser.ToUnanchoredLikePattern(@"100%_test\value");
        Assert.Equal(@"%100\%\_test\\value%", pattern);
    }

    [Fact]
    public void ComputeMatchTier_IsPrefixNeutralAndRanksExactFullNameFirst()
    {
        var texasQuery = SearchQueryParser.Parse("Texas");
        var prefixTier = texasQuery.ComputeMatchTier("Texas A&M University");
        var midStringTier = texasQuery.ComputeMatchTier("The University of Texas at Austin");
        Assert.Equal(1, prefixTier);
        Assert.Equal(1, midStringTier);

        var collegeQuery = SearchQueryParser.Parse("\"Texas College\"");
        Assert.Equal(0, collegeQuery.ComputeMatchTier("Texas College"));
        Assert.Equal(1, collegeQuery.ComputeMatchTier("Central Texas College"));
    }
}
