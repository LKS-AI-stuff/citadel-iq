const MARKER = /\[(\d{1,3}(?:\s*,\s*\d{1,3})*)\]/g;
const MARKER_WITH_LEADING_SPACE = /[ \t]*\[\d{1,3}(?:\s*,\s*\d{1,3})*\]/g;

export type AnswerPart = { type: 'text'; value: string } | { type: 'cite'; numbers: number[] };

/** Splits an answer into text and citation parts. Only numbers in 1..sourceCount are kept; markers with none are dropped. */
export function splitCitations(text: string, sourceCount: number): AnswerPart[] {
  const parts: AnswerPart[] = [];
  let last = 0;

  for (const match of text.matchAll(MARKER)) {
    const index = match.index ?? 0;
    if (index > last) parts.push({ type: 'text', value: text.slice(last, index) });
    last = index + match[0].length;

    const numbers = match[1]
      .split(',')
      .map((n) => Number.parseInt(n.trim(), 10))
      .filter((n) => n >= 1 && n <= sourceCount);
    if (numbers.length > 0) parts.push({ type: 'cite', numbers });
  }

  if (last < text.length) parts.push({ type: 'text', value: text.slice(last) });
  return parts;
}

/** Removes citation markers (and the space before them) — used for past answers sent back as history. */
export function stripCitationMarkers(text: string): string {
  return text.replace(MARKER_WITH_LEADING_SPACE, '');
}

/** DOM id of a source card, so a citation chip can scroll to it. */
export function sourceElementId(turnId: number, sourceNumber: number) {
  return `source-${turnId}-${sourceNumber}`;
}
