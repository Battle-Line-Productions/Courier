"use client";

import { cn } from "@/lib/utils";
import { useEffect, useState } from "react";
import type { DocHeading } from "@/lib/docs";

interface TableOfContentsProps {
  headings: DocHeading[];
}

export function TableOfContents({ headings }: TableOfContentsProps) {
  const [activeId, setActiveId] = useState<string>("");

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            setActiveId(entry.target.id);
          }
        }
      },
      { rootMargin: "-80px 0px -80% 0px" }
    );

    for (const heading of headings) {
      const el = document.getElementById(heading.id);
      if (el) observer.observe(el);
    }

    return () => observer.disconnect();
  }, [headings]);

  if (headings.length === 0) return null;

  return (
    <nav className="space-y-1">
      <h4 className="mb-3 text-sm font-semibold">On This Page</h4>
      {headings.map((heading) => (
        <a
          key={`${heading.id}-${heading.depth}`}
          href={`#${heading.id}`}
          className={cn(
            "block text-sm transition-colors hover:text-foreground",
            heading.depth === 3 && "pl-4",
            heading.depth === 4 && "pl-8",
            activeId === heading.id
              ? "font-medium text-primary"
              : "text-muted-foreground"
          )}
        >
          {heading.text}
        </a>
      ))}
    </nav>
  );
}
