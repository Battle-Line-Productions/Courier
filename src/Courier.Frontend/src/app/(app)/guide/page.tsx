"use client";

import Link from "next/link";
import {
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

const sections = [
  {
    title: "Getting Started",
    description: "Learn how to log in, navigate the interface, and understand the dashboard.",
    href: "/guide/getting-started",
    icon: Rocket,
  },
  {
    title: "Jobs",
    description: "Create and manage file transfer jobs with multi-step pipelines.",
    href: "/guide/jobs",
    icon: Briefcase,
  },
  {
    title: "Connections",
    description: "Configure SFTP, FTP, and other protocol connections for file transfers.",
    href: "/guide/connections",
    icon: Cable,
  },
  {
    title: "Keys",
    description: "Manage PGP encryption keys and SSH authentication keys.",
    href: "/guide/keys",
    icon: KeyRound,
  },
  {
    title: "Chains",
    description: "Link multiple jobs into sequential pipelines with dependency control.",
    href: "/guide/chains",
    icon: Link2,
  },
  {
    title: "Monitors",
    description: "Set up file watchers that automatically trigger jobs when files appear.",
    href: "/guide/monitors",
    icon: Eye,
  },
  {
    title: "Notifications",
    description: "Configure alerts for job successes, failures, and other events.",
    href: "/guide/notifications",
    icon: Bell,
  },
  {
    title: "Tags",
    description: "Organize and categorize your jobs, connections, and other resources.",
    href: "/guide/tags",
    icon: Tag,
  },
  {
    title: "Administration",
    description: "Manage users, review audit logs, and configure system settings.",
    href: "/guide/admin",
    icon: ShieldCheck,
  },
  {
    title: "Developer SDKs",
    description: "Integrate external services with Courier using lightweight NuGet SDK packages.",
    href: "/guide/sdk",
    icon: Code,
  },
];

export default function GuidePage() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">User Guide</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Welcome to the Courier User Guide. Select a topic below to learn how to use
          each feature of the platform.
        </p>
      </div>

      <div className="rounded-lg border bg-primary/5 p-5">
        <h2 className="text-sm font-semibold">What is Courier?</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Courier is an enterprise file transfer and job management platform. It replaces
          manual SFTP, PGP encryption, cron jobs, and ad-hoc scripts with a unified,
          auditable, and secure system. You can define multi-step file transfer jobs,
          schedule them, chain them together, monitor directories for new files, and get
          notified when things succeed or fail — all from a single interface.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {sections.map((section) => (
          <Link
            key={section.href}
            href={section.href}
            className="group rounded-lg border p-5 transition-colors hover:border-primary/40 hover:bg-primary/5"
          >
            <div className="flex items-center gap-3">
              <div className="flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary">
                <section.icon className="h-5 w-5" />
              </div>
              <h3 className="text-sm font-semibold group-hover:text-primary">
                {section.title}
              </h3>
            </div>
            <p className="mt-3 text-sm text-muted-foreground">
              {section.description}
            </p>
          </Link>
        ))}
      </div>
    </div>
  );
}
