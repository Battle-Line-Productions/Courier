"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import type { SidebarSection } from "@/lib/docs";

interface DocsSidebarProps {
  sections: SidebarSection[];
}

export function DocsSidebar({ sections }: DocsSidebarProps) {
  const pathname = usePathname();

  return (
    <nav className="space-y-1">
      <h4 className="mb-3 text-sm font-semibold">Documentation</h4>
      {sections.map((section) => {
        const href = `/docs/${section.slug}`;
        const isActive = pathname === href;

        return (
          <Link
            key={section.slug}
            href={href}
            className={cn(
              "block rounded-md px-3 py-2 text-sm transition-colors",
              isActive
                ? "bg-primary/10 font-medium text-primary"
                : "text-muted-foreground hover:bg-muted hover:text-foreground"
            )}
          >
            {section.title}
          </Link>
        );
      })}
    </nav>
  );
}
