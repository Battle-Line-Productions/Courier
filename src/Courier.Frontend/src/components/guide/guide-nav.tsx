"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import {
  BookOpen,
  Rocket,
  Briefcase,
  Cable,
  KeyRound,
  Link2,
  Eye,
  Bell,
  Tag,
  ShieldCheck,
  Code,
} from "lucide-react";

type NavItem = {
  label: string;
  href: string;
  icon: typeof BookOpen;
  exact?: boolean;
  children?: { label: string; href: string }[];
};

const guideNav: NavItem[] = [
  { label: "Overview", href: "/guide", icon: BookOpen, exact: true },
  { label: "Getting Started", href: "/guide/getting-started", icon: Rocket },
  {
    label: "Jobs",
    href: "/guide/jobs",
    icon: Briefcase,
    children: [
      { label: "Step Type Reference", href: "/guide/jobs/step-types" },
      { label: "File Operations", href: "/guide/jobs/step-types/file-operations" },
      { label: "SFTP Transfer", href: "/guide/jobs/step-types/sftp-transfer" },
      { label: "FTP / FTPS Transfer", href: "/guide/jobs/step-types/ftp-transfer" },
      { label: "PGP Cryptography", href: "/guide/jobs/step-types/pgp-cryptography" },
      { label: "Control Flow", href: "/guide/jobs/step-types/flow-control" },
      { label: "Azure Function", href: "/guide/jobs/step-types/azure-function" },
    ],
  },
  {
    label: "Connections",
    href: "/guide/connections",
    icon: Cable,
    children: [
      { label: "SFTP", href: "/guide/connections/sftp" },
      { label: "FTP", href: "/guide/connections/ftp" },
      { label: "FTPS", href: "/guide/connections/ftps" },
      { label: "Azure Function", href: "/guide/connections/azure-function" },
    ],
  },
  { label: "Keys", href: "/guide/keys", icon: KeyRound },
  { label: "Chains", href: "/guide/chains", icon: Link2 },
  { label: "Monitors", href: "/guide/monitors", icon: Eye },
  { label: "Notifications", href: "/guide/notifications", icon: Bell },
  { label: "Tags", href: "/guide/tags", icon: Tag },
  { label: "Administration", href: "/guide/admin", icon: ShieldCheck },
  {
    label: "Developer SDKs",
    href: "/guide/sdk",
    icon: Code,
    children: [
      { label: "Azure Functions", href: "/guide/sdk/azure-functions" },
    ],
  },
];

export function GuideNav() {
  const pathname = usePathname();

  return (
    <nav className="sticky top-6 space-y-1">
      <h3 className="mb-3 px-3 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        Guide Sections
      </h3>
      {guideNav.map((item) => {
        const isActive = item.exact
          ? pathname === item.href
          : pathname.startsWith(item.href);
        const isExpanded = isActive && item.children;
        return (
          <div key={item.href}>
            <Link
              href={item.href}
              className={cn(
                "flex items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                isActive
                  ? "bg-primary/10 text-primary"
                  : "text-muted-foreground hover:bg-muted hover:text-foreground"
              )}
            >
              <item.icon className="h-4 w-4 shrink-0" />
              {item.label}
            </Link>
            {isExpanded && (
              <div className="ml-5 mt-0.5 space-y-0.5 border-l pl-3">
                {item.children!.map((child) => {
                  const childActive = pathname === child.href;
                  return (
                    <Link
                      key={child.href}
                      href={child.href}
                      className={cn(
                        "block rounded-md px-3 py-1.5 text-xs font-medium transition-colors",
                        childActive
                          ? "bg-primary/10 text-primary"
                          : "text-muted-foreground hover:bg-muted hover:text-foreground"
                      )}
                    >
                      {child.label}
                    </Link>
                  );
                })}
              </div>
            )}
          </div>
        );
      })}
    </nav>
  );
}

export function GuidePrevNext({ prev, next }: {
  prev?: { label: string; href: string };
  next?: { label: string; href: string };
}) {
  return (
    <div className="mt-12 flex items-center justify-between border-t pt-6">
      {prev ? (
        <Link
          href={prev.href}
          className="text-sm font-medium text-primary hover:underline"
        >
          &larr; {prev.label}
        </Link>
      ) : (
        <div />
      )}
      {next ? (
        <Link
          href={next.href}
          className="text-sm font-medium text-primary hover:underline"
        >
          {next.label} &rarr;
        </Link>
      ) : (
        <div />
      )}
    </div>
  );
}
