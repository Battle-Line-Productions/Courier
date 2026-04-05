"use client";

import Link from "next/link";
import { StepCard, type StepDef } from "@/components/guide/step-card";
import { GuidePrevNext } from "@/components/guide/guide-nav";

const steps: StepDef[] = [
  {
    typeKey: "file.copy",
    name: "Copy File",
    description: "Copy files from one location to another on the local filesystem.",
    fields: [
      { name: "source_path", type: "string", required: true, description: "Source file or directory path" },
      { name: "destination_path", type: "string", required: true, description: "Destination file path" },
      { name: "idempotency", type: "string", required: false, description: "Behavior when destination exists: \"overwrite\" (default), \"skip_if_exists\", or \"resume\"" },
    ],
    outputs: ["copied_file", "skipped", "reason"],
  },
  {
    typeKey: "file.move",
    name: "Move File",
    description: "Move files from source to destination (copy then delete source).",
    fields: [
      { name: "source_path", type: "string", required: true, description: "Source file or directory path" },
      { name: "destination_path", type: "string", required: true, description: "Destination file path" },
      { name: "idempotency", type: "string", required: false, description: "Behavior when destination exists: \"overwrite\" (default), \"skip_if_exists\", or \"resume\"" },
    ],
    outputs: ["moved_file", "skipped", "reason"],
  },
  {
    typeKey: "file.delete",
    name: "Delete File",
    description: "Delete a file from the local filesystem.",
    fields: [
      { name: "path", type: "string", required: true, description: "File path to delete" },
      { name: "fail_if_not_found", type: "boolean", required: false, description: "Fail the step if the file does not exist (default: false)" },
    ],
    outputs: ["deleted_file", "existed"],
  },
  {
    typeKey: "file.zip",
    name: "Compress Files",
    description: "Compress files into a ZIP, TAR, or GZIP archive.",
    fields: [
      { name: "source_path / source_paths", type: "string / string[]", required: true, description: "File(s) or directory to compress" },
      { name: "output_path", type: "string", required: true, description: "Output archive file path" },
      { name: "format", type: "string", required: false, description: "Archive format: \"zip\" (default), \"tar\", \"tar.gz\", \"tgz\", \"gzip\", \"gz\"" },
      { name: "password", type: "string", required: false, description: "Optional archive password for encrypted ZIP" },
      { name: "split_max_size_mb", type: "integer", required: false, description: "Split archive into parts if larger than this size in MB" },
    ],
    outputs: ["archive_path", "split_parts", "split_count"],
  },
  {
    typeKey: "file.unzip",
    name: "Extract Archive",
    description: "Extract files from a ZIP, TAR, or GZIP archive.",
    fields: [
      { name: "archive_path", type: "string", required: true, description: "Path to the archive file" },
      { name: "output_directory", type: "string", required: false, description: "Directory to extract into (defaults to workspace)" },
      { name: "format", type: "string", required: false, description: "Archive format: \"zip\" (default), \"tar\", \"tar.gz\", etc." },
      { name: "password", type: "string", required: false, description: "Password for encrypted archives" },
      { name: "verify_integrity", type: "boolean", required: false, description: "Verify archive integrity before extracting (default: true)" },
    ],
    outputs: ["extracted_directory", "extracted_files"],
  },
];

export default function FileOperationsGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">File Operations</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          5 step types for working with files on the local filesystem — copy, move,
          delete, compress, and extract.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/jobs/step-types" className="text-primary hover:underline">
            &larr; Back to Step Types
          </Link>
        </p>
      </div>

      <div className="space-y-4">
        {steps.map((step) => (
          <StepCard key={step.typeKey} step={step} />
        ))}
      </div>

      <GuidePrevNext
        prev={{ label: "Step Types", href: "/guide/jobs/step-types" }}
        next={{ label: "SFTP Transfer", href: "/guide/jobs/step-types/sftp-transfer" }}
      />
    </div>
  );
}
