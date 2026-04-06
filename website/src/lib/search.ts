import type { DocPage } from "./docs";

export interface SearchResult {
  slug: string;
  title: string;
  description: string;
  matchedContent: string;
}

export function searchDocs(
  docs: Array<Pick<DocPage, "slug" | "title" | "description" | "content">>,
  query: string
): SearchResult[] {
  if (!query.trim()) return [];

  const terms = query.toLowerCase().split(/\s+/);

  const results: Array<SearchResult & { score: number }> = [];

  for (const doc of docs) {
    const titleLower = doc.title.toLowerCase();
    const descLower = doc.description.toLowerCase();
    const contentLower = doc.content.toLowerCase();

    let score = 0;

    for (const term of terms) {
      if (titleLower.includes(term)) score += 10;
      if (descLower.includes(term)) score += 5;
      if (contentLower.includes(term)) score += 1;
    }

    if (score === 0) continue;

    // Extract a content snippet around the first match
    let matchedContent = doc.description;
    const contentIndex = contentLower.indexOf(terms[0]);
    if (contentIndex !== -1) {
      const start = Math.max(0, contentIndex - 40);
      const end = Math.min(doc.content.length, contentIndex + 120);
      matchedContent =
        (start > 0 ? "..." : "") +
        doc.content.slice(start, end).replace(/\n/g, " ").trim() +
        (end < doc.content.length ? "..." : "");
    }

    results.push({
      slug: doc.slug,
      title: doc.title,
      description: doc.description,
      matchedContent,
      score,
    });
  }

  return results.sort((a, b) => b.score - a.score).slice(0, 10);
}
