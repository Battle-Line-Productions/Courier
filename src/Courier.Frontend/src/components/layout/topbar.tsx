"use client";

import { usePathname } from "next/navigation";
import Link from "next/link";
import { ChevronRight } from "lucide-react";

function useBreadcrumbs() {
  const pathname = usePathname();
  const segments = pathname.split("/").filter(Boolean);

  const crumbs: { label: string; href: string }[] = [];

  for (let i = 0; i < segments.length; i++) {
    const href = "/" + segments.slice(0, i + 1).join("/");
    let label = segments[i];

    if (label === "jobs") label = "Jobs";
    else if (label === "new") label = "Create";
    else if (label === "edit") label = "Edit";
    else if (label.match(/^[0-9a-f-]{36}$/)) label = "Detail";

    crumbs.push({ label, href });
  }

  return crumbs;
}

export function Topbar() {
  const crumbs = useBreadcrumbs();

  return (
    <header className="flex h-12 items-center border-b bg-background px-6">
      <nav className="flex items-center gap-1 text-sm text-muted-foreground">
        {crumbs.map((crumb, i) => (
          <span key={crumb.href} className="flex items-center gap-1">
            {i > 0 && <ChevronRight className="h-3 w-3" />}
            {i < crumbs.length - 1 ? (
              <Link href={crumb.href} className="hover:text-foreground transition-colors">
                {crumb.label}
              </Link>
            ) : (
              <span className="text-foreground font-medium">{crumb.label}</span>
            )}
          </span>
        ))}
      </nav>
    </header>
  );
}
