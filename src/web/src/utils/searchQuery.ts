export interface ParsedSearchQuery {
  rawQuery: string
  normalizedFullText: string
  exactPhrases: string[]
  fuzzyTerms: string[]
  effectiveLength: number
  isEmpty: boolean
}

export const MAX_SEARCH_TERMS = 16

const EMPTY_PARSED_QUERY: ParsedSearchQuery = {
  rawQuery: '',
  normalizedFullText: '',
  exactPhrases: [],
  fuzzyTerms: [],
  effectiveLength: 0,
  isEmpty: true,
}

function collapseWhitespace(input: string): string {
  return input.trim().replace(/\s+/g, ' ')
}

function escapeRegExp(input: string): string {
  return input.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

function containsIgnoreCase(list: readonly string[], candidate: string): boolean {
  const lower = candidate.toLowerCase()
  return list.some((item) => item.toLowerCase() === lower)
}

function buildExactPhraseRegex(phrase: string): RegExp {
  const escapedWords = phrase
    .split(/\s+/)
    .filter(Boolean)
    .map(escapeRegExp)
  return new RegExp(`(?<![\\p{L}\\p{N}])${escapedWords.join('\\s+')}(?![\\p{L}\\p{N}])`, 'iu')
}

/**
 * Parses a search string into double-quoted exact word-boundary phrases ("...")
 * and unanchored substring tokens (*term*). Unclosed trailing quotes fall back
 * to fuzzy tokens while the user is actively typing.
 */
export function parseSearchQuery(rawQuery: string | null | undefined): ParsedSearchQuery {
  if (!rawQuery || !rawQuery.trim()) {
    return EMPTY_PARSED_QUERY
  }

  // Normalize typographic smart double quotes (common on macOS/iOS) to ASCII '"'
  const normalized = rawQuery.replace(/[\u201C\u201D]/g, '"').trim()

  const exactPhrases: string[] = []
  let remainder = ''

  let i = 0
  while (i < normalized.length) {
    if (normalized[i] === '"') {
      const closingIdx = normalized.indexOf('"', i + 1)
      if (closingIdx >= 0) {
        const collapsed = collapseWhitespace(normalized.slice(i + 1, closingIdx))
        if (
          collapsed.length > 0 &&
          exactPhrases.length < MAX_SEARCH_TERMS &&
          !containsIgnoreCase(exactPhrases, collapsed)
        ) {
          exactPhrases.push(collapsed)
        }
        remainder += ' '
        i = closingIdx + 1
        continue
      }

      // Unclosed trailing quote mid-typing: treat remaining text as fuzzy tokens
      remainder += ' '
      i += 1
      continue
    }

    remainder += normalized[i]
    i += 1
  }

  const fuzzyTerms: string[] = []
  const rawTokens = remainder.split(/[\s,;]+/).filter(Boolean)

  for (const rawToken of rawTokens) {
    if (exactPhrases.length + fuzzyTerms.length >= MAX_SEARCH_TERMS) {
      break
    }

    const token = rawToken.replace(/^[,;\u2014\u2013]+|[,;\u2014\u2013]+$/g, '')
    if (!token || token === '-' || token === '\u2014' || token === '\u2013') {
      continue
    }

    if (!containsIgnoreCase(fuzzyTerms, token) && !containsIgnoreCase(exactPhrases, token)) {
      fuzzyTerms.push(token)
    }
  }

  if (exactPhrases.length === 0 && fuzzyTerms.length === 0) {
    return EMPTY_PARSED_QUERY
  }

  let effectiveLength = 0
  for (const phrase of exactPhrases) {
    effectiveLength += phrase.replace(/\s+/g, '').length
  }
  for (const term of fuzzyTerms) {
    effectiveLength += term.length
  }

  const normalizedFullText =
    exactPhrases.length === 1 && fuzzyTerms.length === 0
      ? exactPhrases[0]!
      : collapseWhitespace(normalized.replace(/"/g, ' '))

  return {
    rawQuery: normalized,
    normalizedFullText,
    exactPhrases,
    fuzzyTerms,
    effectiveLength,
    isEmpty: false,
  }
}

export function getEffectiveSearchLength(rawQuery: string | null | undefined): number {
  return parseSearchQuery(rawQuery).effectiveLength
}

export function containsExactPhrase(text: string, phrase: string): boolean {
  if (!text || !phrase) return false
  return buildExactPhraseRegex(phrase).test(text)
}

/**
 * Evaluates whether every quoted phrase matches on word boundaries and every
 * unquoted token matches as an unanchored substring across the candidate fields.
 */
export function matchesSearchQuery(
  queryOrParsed: string | ParsedSearchQuery,
  fields: ReadonlyArray<string | null | undefined>
): boolean {
  const parsed =
    typeof queryOrParsed === 'string' ? parseSearchQuery(queryOrParsed) : queryOrParsed

  if (parsed.isEmpty) {
    return true
  }

  const validFields = fields.filter((f): f is string => Boolean(f))
  if (validFields.length === 0) {
    return false
  }

  for (const phrase of parsed.exactPhrases) {
    const rx = buildExactPhraseRegex(phrase)
    if (!validFields.some((field) => rx.test(field))) {
      return false
    }
  }

  const lowerFields = validFields.map((f) => f.toLowerCase())
  for (const term of parsed.fuzzyTerms) {
    const lowerTerm = term.toLowerCase()
    if (!lowerFields.some((field) => field.includes(lowerTerm))) {
      return false
    }
  }

  return true
}
