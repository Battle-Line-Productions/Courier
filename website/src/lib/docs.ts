import fs from "fs";
import path from "path";
import matter from "gray-matter";

const DOCS_DIR = path.join(process.cwd(), "docs-content");

export interface DocPage {
  slug: string;
  title: string;
  description: string;
  order: number;
  keywords: string[];
  content: string;
  headings: DocHeading[];
}

export interface DocHeading {
  depth: number;
  text: string;
  id: string;
}

function extractHeadings(content: string): DocHeading[] {
  const headingRegex = /^(#{2,4})\s+(.+)$/gm;
  const headings: DocHeading[] = [];
  let match: RegExpExecArray | null;

  while ((match = headingRegex.exec(content)) !== null) {
    const text = match[2].trim();
    headings.push({
      depth: match[1].length,
      text,
      id: text
        .toLowerCase()
        .replace(/[^\w\s-]/g, "")
        .replace(/\s+/g, "-"),
    });
  }

  return headings;
}

export function getAllDocs(): DocPage[] {
  if (!fs.existsSync(DOCS_DIR)) {
    console.warn("docs-content/ not found — run sync-docs first");
    return [];
  }

  const files = fs.readdirSync(DOCS_DIR).filter((f) => f.endsWith(".md"));

  const docs = files.map((file) => {
    const raw = fs.readFileSync(path.join(DOCS_DIR, file), "utf-8");
    const { data, content } = matter(raw);

    return {
      slug: file.replace(/\.md$/, ""),
      title: data.title || file.replace(/\.md$/, ""),
      description: data.description || "",
      order: data.order || 99,
      keywords: data.keywords || [],
      content,
      headings: extractHeadings(content),
    } satisfies DocPage;
  });

  return docs.sort((a, b) => a.order - b.order);
}

export function getDocBySlug(slug: string): DocPage | undefined {
  const docs = getAllDocs();
  return docs.find((d) => d.slug === slug);
}

export function getDocSlugs(): string[] {
  return getAllDocs().map((d) => d.slug);
}

export interface SidebarSection {
  title: string;
  slug: string;
}

export function getDocsSidebar(): SidebarSection[] {
  return getAllDocs().map((d) => ({
    title: d.title,
    slug: d.slug,
  }));
}
