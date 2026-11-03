using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;

namespace GradCast.Api.Services;

/// <summary>
/// Parses user search input into unanchored substring tokens (*term*) and
/// double-quoted exact word-boundary phrases ("exact phrase"), with SQLite LIKE
/// wildcard escaping and prefix-neutral relevance tiering.
/// </summary>
public static class SearchQueryParser
{
    public const int MaxQueryLength = 200;
    public const int MaxTerms = 16;
    public const char SqlLikeEscapeChar = '\\';

    private static readonly FrozenSet<string> AllowedStateCodes = new[]
    {
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
        "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
        "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
        "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
        "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
        "DC", "PR", "VI", "GU", "AS", "MP", "FM", "MH", "PW"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly char[] UnquotedSplitDelimiters = [' ', '\t', '\r', '\n', ',', ';'];
    private static readonly char[] TokenTrimChars = [',', ';', '\u2014', '\u2013'];
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);

    public static bool IsValidStateCode(string? state) =>
        !string.IsNullOrWhiteSpace(state) && AllowedStateCodes.Contains(state.Trim());

    public static ParsedSearchQuery Parse(string? rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return ParsedSearchQuery.Empty;
        }

        // Normalize typographic smart double quotes (common on macOS/iOS) to ASCII '"'
        var normalized = rawQuery
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Trim();

        var exactPhrases = new List<string>();
        var remainder = new StringBuilder(normalized.Length);

        var i = 0;
        while (i < normalized.Length)
        {
            if (normalized[i] == '"')
            {
                var closingIdx = normalized.IndexOf('"', i + 1);
                if (closingIdx >= 0)
                {
                    var rawPhrase = normalized[(i + 1)..closingIdx];
                    var collapsedPhrase = CollapseWhitespace(rawPhrase);
                    if (collapsedPhrase.Length > 0 &&
                        exactPhrases.Count < MaxTerms &&
                        !ContainsIgnoreCase(exactPhrases, collapsedPhrase))
                    {
                        exactPhrases.Add(collapsedPhrase);
                    }

                    remainder.Append(' ');
                    i = closingIdx + 1;
                    continue;
                }

                // Unclosed trailing quote while the user is still typing:
                // treat remaining text as unquoted fuzzy tokens so live preview works smoothly.
                remainder.Append(' ');
                i++;
                continue;
            }

            remainder.Append(normalized[i]);
            i++;
        }

        var fuzzyTerms = new List<string>();
        var rawTokens = remainder
            .ToString()
            .Split(UnquotedSplitDelimiters, StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawToken in rawTokens)
        {
            if (exactPhrases.Count + fuzzyTerms.Count >= MaxTerms)
            {
                break;
            }

            var token = rawToken.Trim(TokenTrimChars);
            if (token.Length == 0 || token is "-" or "\u2014" or "\u2013")
            {
                continue;
            }

            if (!ContainsIgnoreCase(fuzzyTerms, token) &&
                !ContainsIgnoreCase(exactPhrases, token))
            {
                fuzzyTerms.Add(token);
            }
        }

        if (exactPhrases.Count == 0 && fuzzyTerms.Count == 0)
        {
            return ParsedSearchQuery.Empty;
        }

        var normalizedFullText = exactPhrases.Count == 1 && fuzzyTerms.Count == 0
            ? exactPhrases[0]
            : CollapseWhitespace(normalized.Replace('"', ' '));

        return new ParsedSearchQuery(normalized, normalizedFullText, exactPhrases, fuzzyTerms);
    }

    public static int GetEffectiveLength(string? rawQuery) => Parse(rawQuery).EffectiveLength;

    /// <summary>
    /// Escapes SQL LIKE wildcard characters (\, %, _) and wraps the term in %...%
    /// for unanchored substring matching with <c>ESCAPE '\'</c>.
    /// </summary>
    public static string ToUnanchoredLikePattern(string term)
    {
        var sb = new StringBuilder(term.Length + 4);
        sb.Append('%');
        foreach (var ch in term)
        {
            if (ch is '\\' or '%' or '_')
            {
                sb.Append(SqlLikeEscapeChar);
            }
            sb.Append(ch);
        }
        sb.Append('%');
        return sb.ToString();
    }

    internal static Regex BuildExactPhraseRegex(string phrase)
    {
        var escapedWords = phrase
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Regex.Escape);
        var pattern = $"(?<![\\p{{L}}\\p{{N}}]){string.Join("\\s+", escapedWords)}(?![\\p{{L}}\\p{{N}}])";
        return new Regex(
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexMatchTimeout);
    }

    private static string CollapseWhitespace(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var parts = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    private static bool ContainsIgnoreCase(List<string> items, string candidate)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i], candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}

public sealed class ParsedSearchQuery
{
    public static readonly ParsedSearchQuery Empty = new(
        string.Empty,
        string.Empty,
        Array.Empty<string>(),
        Array.Empty<string>());

    private readonly Regex[] _exactPhraseMatchers;

    public ParsedSearchQuery(
        string rawQuery,
        string normalizedFullText,
        IReadOnlyList<string> exactPhrases,
        IReadOnlyList<string> fuzzyTerms)
    {
        RawQuery = rawQuery;
        NormalizedFullText = normalizedFullText;
        ExactPhrases = exactPhrases;
        FuzzyTerms = fuzzyTerms;
        _exactPhraseMatchers = exactPhrases
            .Select(SearchQueryParser.BuildExactPhraseRegex)
            .ToArray();

        var len = 0;
        foreach (var phrase in exactPhrases)
        {
            foreach (var ch in phrase)
            {
                if (!char.IsWhiteSpace(ch))
                {
                    len++;
                }
            }
        }
        foreach (var term in fuzzyTerms)
        {
            len += term.Length;
        }
        EffectiveLength = len;
    }

    public string RawQuery { get; }
    public string NormalizedFullText { get; }
    public IReadOnlyList<string> ExactPhrases { get; }
    public IReadOnlyList<string> FuzzyTerms { get; }
    public int EffectiveLength { get; }
    public bool IsEmpty => ExactPhrases.Count == 0 && FuzzyTerms.Count == 0;

    /// <summary>
    /// All SQL candidate substring terms (both exact phrases and fuzzy tokens)
    /// that must appear as substrings in at least one searchable column.
    /// </summary>
    public IEnumerable<string> AllSqlFilterTerms => ExactPhrases.Concat(FuzzyTerms);

    /// <summary>
    /// Verifies that every double-quoted phrase matches on Unicode word boundaries
    /// and every unquoted token matches as an unanchored case-insensitive substring
    /// across at least one of the provided fields.
    /// </summary>
    public bool Matches(params ReadOnlySpan<string?> fields)
    {
        if (IsEmpty)
        {
            return false;
        }

        foreach (var phraseRegex in _exactPhraseMatchers)
        {
            var matched = false;
            foreach (var field in fields)
            {
                if (!string.IsNullOrEmpty(field) && phraseRegex.IsMatch(field))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                return false;
            }
        }

        foreach (var term in FuzzyTerms)
        {
            var matched = false;
            foreach (var field in fields)
            {
                if (!string.IsNullOrEmpty(field) &&
                    field.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Computes a prefix-neutral relevance tier (0 = best, 3 = secondary-field-only).
    /// Never privileges index-0 string prefixes over mid-name matches (e.g. "The University of...").
    /// </summary>
    public int ComputeMatchTier(string primaryField, string? alternateExactPrimaryField = null)
    {
        var trimmedPrimary = primaryField.Trim();
        if (NormalizedFullText.Length > 0)
        {
            if (string.Equals(trimmedPrimary, NormalizedFullText, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            if (!string.IsNullOrWhiteSpace(alternateExactPrimaryField) &&
                string.Equals(alternateExactPrimaryField.Trim(), NormalizedFullText, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
        }

        var allInPrimary = true;
        var anyInPrimary = false;

        foreach (var phraseRegex in _exactPhraseMatchers)
        {
            if (phraseRegex.IsMatch(trimmedPrimary))
            {
                anyInPrimary = true;
            }
            else
            {
                allInPrimary = false;
            }
        }

        foreach (var term in FuzzyTerms)
        {
            if (trimmedPrimary.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                anyInPrimary = true;
            }
            else
            {
                allInPrimary = false;
            }
        }

        if (allInPrimary)
        {
            return 1;
        }

        return anyInPrimary ? 2 : 3;
    }
}
