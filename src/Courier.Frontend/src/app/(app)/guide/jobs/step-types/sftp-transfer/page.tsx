"use client";

import Link from "next/link";
import { StepCard, type StepDef } from "@/components/guide/step-card";
import { GuidePrevNext } from "@/components/guide/guide-nav";

const steps: StepDef[] = [
  {
    typeKey: "sftp.upload",
    name: "SFTP Upload",
    description: "Upload a local file to a remote SFTP server.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the SFTP connection to use" },
      { name: "local_path", type: "string", required: true, description: "Local file path to upload" },
      { name: "remote_path", type: "string", required: true, description: "Remote destination path" },
      { name: "idempotency", type: "string", required: false, description: "\"overwrite\" (default) or \"skip_if_exists\"" },
      { name: "atomic_upload", type: "boolean", required: false, description: "Write to temp file then rename for safety (default: true)" },
      { name: "atomic_suffix", type: "string", required: false, description: "Temporary suffix during atomic upload (default: \".tmp\")" },
      { name: "resume_partial", type: "boolean", required: false, description: "Resume incomplete uploads (default: false)" },
    ],
    outputs: ["uploaded_file", "skipped", "reason"],
  },
  {
    typeKey: "sftp.download",
    name: "SFTP Download",
    description: "Download a file from a remote SFTP server to local storage.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the SFTP connection to use" },
      { name: "remote_path", type: "string", required: true, description: "Remote file path to download" },
      { name: "local_path", type: "string", required: false, description: "Local destination path (defaults to workspace)" },
      { name: "idempotency", type: "string", required: false, description: "\"overwrite\" (default) or \"skip_if_exists\"" },
      { name: "resume_partial", type: "boolean", required: false, description: "Resume incomplete downloads (default: false)" },
      { name: "file_pattern", type: "string", required: false, description: "Glob pattern to filter files (default: \"*\")" },
      { name: "delete_after_download", type: "boolean", required: false, description: "Delete remote file after successful download (default: false)" },
    ],
    outputs: ["downloaded_file", "skipped", "reason"],
  },
  {
    typeKey: "sftp.list",
    name: "SFTP List Directory",
    description: "List files in a remote SFTP directory. Useful before a foreach loop.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the SFTP connection to use" },
      { name: "remote_path", type: "string", required: true, description: "Remote directory path to list" },
      { name: "file_pattern", type: "string", required: false, description: "Glob pattern to filter results (e.g., \"*.csv\")" },
    ],
    outputs: ["file_list", "file_count"],
  },
  {
    typeKey: "sftp.mkdir",
    name: "SFTP Create Directory",
    description: "Create a directory on a remote SFTP server.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the SFTP connection to use" },
      { name: "remote_path", type: "string", required: true, description: "Remote directory path to create" },
    ],
    outputs: ["created_directory"],
  },
  {
    typeKey: "sftp.rmdir",
    name: "SFTP Remove Directory",
    description: "Remove a directory from a remote SFTP server.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the SFTP connection to use" },
      { name: "remote_path", type: "string", required: true, description: "Remote directory path to remove" },
      { name: "recursive", type: "boolean", required: false, description: "Remove contents recursively (default: false)" },
    ],
    outputs: [],
  },
];

export default function SftpTransferGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">SFTP Transfer Steps</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          5 step types for transferring files and managing directories on remote SFTP
          servers. All require an SFTP connection configured in the{" "}
          <Link href="/guide/connections/sftp" className="text-primary hover:underline">Connections</Link> page.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/jobs/step-types" className="text-primary hover:underline">&larr; Back to Step Types</Link>
        </p>
      </div>

      <div className="space-y-4">
        {steps.map((step) => (
          <StepCard key={step.typeKey} step={step} />
        ))}
      </div>

      <GuidePrevNext
        prev={{ label: "File Operations", href: "/guide/jobs/step-types/file-operations" }}
        next={{ label: "FTP / FTPS Transfer", href: "/guide/jobs/step-types/ftp-transfer" }}
      />
    </div>
  );
}
