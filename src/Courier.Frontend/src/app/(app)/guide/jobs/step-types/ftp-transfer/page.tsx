"use client";

import Link from "next/link";
import { StepCard, type StepDef } from "@/components/guide/step-card";
import { GuidePrevNext } from "@/components/guide/guide-nav";

const ftpSteps: StepDef[] = [
  {
    typeKey: "ftp.upload",
    name: "FTP Upload",
    description: "Upload a local file to a remote FTP server.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the FTP connection to use" },
      { name: "local_path", type: "string", required: true, description: "Local file path to upload" },
      { name: "remote_path", type: "string", required: true, description: "Remote destination path" },
      { name: "idempotency", type: "string", required: false, description: "\"overwrite\" (default) or \"skip_if_exists\"" },
      { name: "atomic_upload", type: "boolean", required: false, description: "Write to temp file then rename (default: true)" },
      { name: "atomic_suffix", type: "string", required: false, description: "Temp suffix for atomic upload (default: \".tmp\")" },
      { name: "resume_partial", type: "boolean", required: false, description: "Resume incomplete uploads (default: false)" },
    ],
    outputs: ["uploaded_file", "skipped", "reason"],
  },
  {
    typeKey: "ftp.download",
    name: "FTP Download",
    description: "Download a file from a remote FTP server.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the FTP connection to use" },
      { name: "remote_path", type: "string", required: true, description: "Remote file path to download" },
      { name: "local_path", type: "string", required: false, description: "Local destination path (defaults to workspace)" },
      { name: "idempotency", type: "string", required: false, description: "\"overwrite\" (default) or \"skip_if_exists\"" },
      { name: "resume_partial", type: "boolean", required: false, description: "Resume incomplete downloads (default: false)" },
      { name: "file_pattern", type: "string", required: false, description: "Glob pattern to filter files (default: \"*\")" },
      { name: "delete_after_download", type: "boolean", required: false, description: "Delete remote file after download (default: false)" },
    ],
    outputs: ["downloaded_file", "skipped", "reason"],
  },
  {
    typeKey: "ftp.list",
    name: "FTP List Directory",
    description: "List files in a remote FTP directory.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the FTP connection to use" },
      { name: "remote_path", type: "string", required: true, description: "Remote directory path to list" },
      { name: "file_pattern", type: "string", required: false, description: "Glob pattern to filter results" },
    ],
    outputs: ["file_list", "file_count"],
  },
  {
    typeKey: "ftp.mkdir",
    name: "FTP Create Directory",
    description: "Create a directory on a remote FTP server.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the FTP connection to use" },
      { name: "remote_path", type: "string", required: true, description: "Remote directory path to create" },
    ],
    outputs: ["created_directory"],
  },
  {
    typeKey: "ftp.rmdir",
    name: "FTP Remove Directory",
    description: "Remove a directory from a remote FTP server.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the FTP connection to use" },
      { name: "remote_path", type: "string", required: true, description: "Remote directory path to remove" },
      { name: "recursive", type: "boolean", required: false, description: "Remove contents recursively (default: false)" },
    ],
    outputs: [],
  },
];

export default function FtpTransferGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">FTP / FTPS Transfer Steps</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          10 step types for transferring files via FTP and FTPS. The FTPS variants
          (ftps.upload, ftps.download, etc.) have identical configuration fields — the
          only difference is they require an{" "}
          <Link href="/guide/connections/ftps" className="text-primary hover:underline">FTPS connection</Link>{" "}
          which uses TLS encryption.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/jobs/step-types" className="text-primary hover:underline">&larr; Back to Step Types</Link>
        </p>
      </div>

      <div className="rounded-lg border-l-4 border-muted-foreground/30 bg-muted/30 p-4">
        <p className="text-sm text-muted-foreground">
          <strong>FTPS Note:</strong> For each FTP step below, there is an identical FTPS
          counterpart (e.g., <code className="rounded bg-muted px-1 py-0.5 text-xs">ftps.upload</code>,{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">ftps.download</code>, etc.)
          with the same fields. Use the FTPS variant when your connection uses TLS encryption.
        </p>
      </div>

      <div className="space-y-4">
        {ftpSteps.map((step) => (
          <StepCard key={step.typeKey} step={step} />
        ))}
      </div>

      <GuidePrevNext
        prev={{ label: "SFTP Transfer", href: "/guide/jobs/step-types/sftp-transfer" }}
        next={{ label: "PGP Cryptography", href: "/guide/jobs/step-types/pgp-cryptography" }}
      />
    </div>
  );
}
