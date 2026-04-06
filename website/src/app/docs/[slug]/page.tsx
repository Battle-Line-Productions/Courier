import { notFound } from "next/navigation";
import { MDXRemote } from "next-mdx-remote/rsc";
import remarkGfm from "remark-gfm";
import rehypeSlug from "rehype-slug";
import rehypeHighlight from "rehype-highlight";
import { getAllDocs, getDocBySlug, getDocSlugs } from "@/lib/docs";
import { buildDocMetadata } from "@/lib/metadata";
import { mdxComponents } from "@/components/docs/mdx-components";
import { TableOfContents } from "@/components/docs/table-of-contents";
import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import type { Metadata } from "next";

interface DocPageProps {
  params: Promise<{ slug: string }>;
}

export async function generateStaticParams() {
  return getDocSlugs().map((slug) => ({ slug }));
}

export async function generateMetadata({
  params,
}: DocPageProps): Promise<Metadata> {
  const { slug } = await params;
  const doc = getDocBySlug(slug);
  if (!doc) return {};

  return buildDocMetadata(slug, {
    title: doc.title,
    description: doc.description,
    keywords: doc.keywords,
  });
}

export default async function DocPage({ params }: DocPageProps) {
  const { slug } = await params;
  const doc = getDocBySlug(slug);

  if (!doc) {
    notFound();
  }

  // Get prev/next for navigation
  const allDocs = getAllDocs();
  const currentIndex = allDocs.findIndex((d) => d.slug === slug);
  const prev = currentIndex > 0 ? allDocs[currentIndex - 1] : null;
  const next =
    currentIndex < allDocs.length - 1 ? allDocs[currentIndex + 1] : null;

  return (
    <div className="flex gap-8">
      {/* Doc content */}
      <article className="min-w-0 flex-1">
        <h1 className="text-3xl font-bold tracking-tight">{doc.title}</h1>
        <p className="mt-2 text-lg text-muted-foreground">{doc.description}</p>

        <div className="mt-8">
          <MDXRemote
            source={doc.content}
            components={mdxComponents}
            options={{
              mdxOptions: {
                remarkPlugins: [remarkGfm],
                rehypePlugins: [rehypeSlug, rehypeHighlight],
              },
            }}
          />
        </div>

        {/* Prev / Next navigation */}
        <nav className="mt-12 flex items-center justify-between border-t pt-6">
          {prev ? (
            <Link
              href={`/docs/${prev.slug}`}
              className="group flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
            >
              <ChevronLeft className="h-4 w-4" />
              <div>
                <div className="text-xs text-muted-foreground">Previous</div>
                <div className="font-medium group-hover:text-primary">
                  {prev.title}
                </div>
              </div>
            </Link>
          ) : (
            <div />
          )}
          {next ? (
            <Link
              href={`/docs/${next.slug}`}
              className="group flex items-center gap-2 text-right text-sm text-muted-foreground hover:text-foreground"
            >
              <div>
                <div className="text-xs text-muted-foreground">Next</div>
                <div className="font-medium group-hover:text-primary">
                  {next.title}
                </div>
              </div>
              <ChevronRight className="h-4 w-4" />
            </Link>
          ) : (
            <div />
          )}
        </nav>
      </article>

      {/* Table of contents — hidden on smaller screens */}
      <aside className="hidden w-56 shrink-0 xl:block">
        <div className="sticky top-20">
          <TableOfContents headings={doc.headings} />
        </div>
      </aside>
    </div>
  );
}
