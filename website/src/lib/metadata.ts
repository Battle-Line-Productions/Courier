import type { Metadata } from "next";
import { PAGE_KEYWORDS } from "./keywords";

const SITE_URL = "https://couriermft.com";
const SITE_NAME = "Courier MFT";

interface MetadataOptions {
  title: string;
  description: string;
  keywords?: string[];
  path?: string;
}

export function buildMetadata({
  title,
  description,
  keywords = [],
  path = "",
}: MetadataOptions): Metadata {
  const url = `${SITE_URL}${path}`;

  return {
    title,
    description,
    keywords,
    alternates: { canonical: url },
    openGraph: {
      title: `${title} | ${SITE_NAME}`,
      description,
      url,
      siteName: SITE_NAME,
      type: "website",
    },
    twitter: {
      card: "summary_large_image",
      title: `${title} | ${SITE_NAME}`,
      description,
    },
  };
}

export function buildDocMetadata(
  slug: string,
  frontmatter: { title: string; description: string; keywords: string[] }
): Metadata {
  return buildMetadata({
    title: frontmatter.title,
    description: frontmatter.description,
    keywords: frontmatter.keywords,
    path: `/docs/${slug}`,
  });
}

export { PAGE_KEYWORDS, SITE_URL, SITE_NAME };
