"use client";

import Link from "next/link";
import { GuidePrevNext } from "@/components/guide/guide-nav";
import { FileIcon, Upload, Lock, GitBranch, Cloud } from "lucide-react";

const categories = [
  {
    title: "File Operations",
    href: "/guide/jobs/step-types/file-operations",
    icon: FileIcon,
    count: 5,
    types: ["file.copy", "file.move", "file.delete", "file.zip", "file.unzip"],
    description: "Copy, move, delete, compress, and extract files on the local filesystem.",
  },
  {
    title: "SFTP Transfer",
    href: "/guide/jobs/step-types/sftp-transfer",
    icon: Upload,
    count: 5,
    types: ["sftp.upload", "sftp.download", "sftp.list", "sftp.mkdir", "sftp.rmdir"],
    description: "Upload, download, and manage files on remote SFTP servers over SSH.",
  },
  {
    title: "FTP / FTPS Transfer",
    href: "/guide/jobs/step-types/ftp-transfer",
    icon: Upload,
    count: 10,
    types: ["ftp.upload", "ftp.download", "ftp.list", "ftp.mkdir", "ftp.rmdir", "ftps.*"],
    description: "Upload, download, and manage files via FTP or FTP over TLS. FTPS steps have identical fields.",
  },
  {
    title: "PGP Cryptography",
    href: "/guide/jobs/step-types/pgp-cryptography",
    icon: Lock,
    count: 4,
    types: ["pgp.encrypt", "pgp.decrypt", "pgp.sign", "pgp.verify"],
    description: "Encrypt, decrypt, sign, and verify files using PGP keys managed in Courier.",
  },
  {
    title: "Control Flow",
    href: "/guide/jobs/step-types/flow-control",
    icon: GitBranch,
    count: 4,
    types: ["flow.if", "flow.else", "flow.foreach", "flow.end"],
    description: "Add conditional logic and loops to your job pipelines.",
  },
  {
    title: "Azure Functions",
    href: "/guide/jobs/step-types/azure-function",
    icon: Cloud,
    count: 1,
    types: ["azure_function.execute"],
    description: "Invoke Azure Functions with optional callback for long-running operations.",
  },
];

export default function StepTypesIndex() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Step Type Reference</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Courier supports 29 step types across 6 categories. Each step type has its
          own configuration fields, required parameters, and output values that can be
          passed to subsequent steps.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/jobs" className="text-primary hover:underline">
            &larr; Back to Jobs Guide
          </Link>
        </p>
      </div>

      {/* Context reference tip */}
      <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
        <p className="text-sm">
          <strong>Context References:</strong> String fields support the{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">context:</code> prefix
          to reference outputs from previous steps. For example,{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">
            context:download-files.downloaded_file
          </code>{" "}
          uses the output from a step with the alias &quot;download-files&quot;.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        {categories.map((cat) => (
          <Link
            key={cat.href}
            href={cat.href}
            className="group rounded-lg border p-5 transition-colors hover:border-primary/40 hover:bg-primary/5"
          >
            <div className="flex items-center gap-3">
              <div className="flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary">
                <cat.icon className="h-5 w-5" />
              </div>
              <div>
                <h3 className="text-sm font-semibold group-hover:text-primary">
                  {cat.title}
                </h3>
                <p className="text-xs text-muted-foreground">
                  {cat.count} step {cat.count === 1 ? "type" : "types"}
                </p>
              </div>
            </div>
            <p className="mt-3 text-sm text-muted-foreground">{cat.description}</p>
            <div className="mt-3 flex flex-wrap gap-1">
              {cat.types.map((t) => (
                <code
                  key={t}
                  className="rounded bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground"
                >
                  {t}
                </code>
              ))}
            </div>
          </Link>
        ))}
      </div>

      <GuidePrevNext
        prev={{ label: "Jobs", href: "/guide/jobs" }}
        next={{ label: "Connections", href: "/guide/connections" }}
      />
    </div>
  );
}
