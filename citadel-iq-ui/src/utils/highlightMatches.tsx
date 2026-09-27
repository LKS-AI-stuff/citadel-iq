function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** Best-effort literal highlighting of the search terms within a semantic-search result snippet.
 * Since results are matched by meaning, not exact words, this only highlights terms that happen
 * to appear verbatim — it's a scanning aid, not a claim that highlighted words caused the match. */
export function highlightMatches(text: string, query: string) {
  const terms = Array.from(new Set(query.split(/\s+/).filter((word) => word.length >= 3))).map(escapeRegExp);

  if (terms.length === 0) {
    return text;
  }

  const pattern = new RegExp(`(${terms.join('|')})`, 'gi');
  const parts = text.split(pattern);

  // A single capturing group in the split pattern means matches land at odd indices.
  return parts.map((part, index) =>
    index % 2 === 1 ? (
      <mark key={index} style={{ backgroundColor: 'rgba(37, 99, 235, 0.2)', borderRadius: 2, padding: '0 2px' }}>
        {part}
      </mark>
    ) : (
      part
    ),
  );
}
