import Link from "next/link";
import { getAllDocs } from "@/lib/docs";
import { buildMetadata } from "@/lib/metadata";
import type { Metadata } from "next";

export const metadata: Metadata = buildMetadata({
  title: "Documentation",
  description:
    "Comprehensive documentation for Courier MFT — architecture, job engine, connections, encryption, API reference, and deployment guides.",
  keywords: [
    "Courier MFT documentation",
    "managed file transfer docs",
    "MFT setup guide",
  ],
  path: "/docs",
});

export default function DocsIndexPage() {
  const docs = getAllDocs();

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight">Documentation</h1>
      <p className="mt-4 text-lg text-muted-foreground">
        Everything you need to get started with Courier MFT, understand its
        architecture, and deploy it in production.
      </p>
      <div className="mt-8 grid gap-4 sm:grid-cols-2">
        {docs.map((doc) => (
          <Link
            key={doc.slug}
            href={`/docs/${doc.slug}`}
            className="group rounded-lg border bg-card p-5 transition-colors hover:border-primary/50"
          >
            <h2 className="font-semibold group-hover:text-primary">
              {doc.title}
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">
              {doc.description}
            </p>
          </Link>
        ))}
      </div>
    </div>
  );
}
