import {
  File,
  Shield,
  Globe,
  Lock,
  KeyRound,
  Cloud,
  GitBranch,
  type LucideIcon,
} from "lucide-react";
import type { StepFormData } from "./step-builder";
import { parseStepConfig } from "./step-config-form";

export const STEP_TYPE_GROUPS = [
  {
    label: "File Operations",
    category: "file",
    types: [
      { value: "file.copy", label: "File Copy", description: "Copy files between directories" },
      { value: "file.move", label: "File Move", description: "Move files to a new location" },
      { value: "file.zip", label: "File Zip (Compress)", description: "Compress files into a ZIP archive" },
      { value: "file.unzip", label: "File Unzip (Extract)", description: "Extract files from a ZIP archive" },
      { value: "file.delete", label: "File Delete", description: "Delete files from disk" },
    ],
  },
  {
    label: "SFTP",
    category: "sftp",
    types: [
      { value: "sftp.upload", label: "SFTP Upload", description: "Upload files via SFTP" },
      { value: "sftp.download", label: "SFTP Download", description: "Download files via SFTP" },
      { value: "sftp.list", label: "SFTP List", description: "List remote directory contents" },
      { value: "sftp.mkdir", label: "SFTP Create Directory", description: "Create a remote directory" },
      { value: "sftp.rmdir", label: "SFTP Remove Directory", description: "Remove a remote directory" },
    ],
  },
  {
    label: "FTP",
    category: "ftp",
    types: [
      { value: "ftp.upload", label: "FTP Upload", description: "Upload files via FTP" },
      { value: "ftp.download", label: "FTP Download", description: "Download files via FTP" },
      { value: "ftp.list", label: "FTP List", description: "List remote directory contents" },
      { value: "ftp.mkdir", label: "FTP Create Directory", description: "Create a remote directory" },
      { value: "ftp.rmdir", label: "FTP Remove Directory", description: "Remove a remote directory" },
    ],
  },
  {
    label: "FTPS",
    category: "ftps",
    types: [
      { value: "ftps.upload", label: "FTPS Upload", description: "Upload files via FTPS" },
      { value: "ftps.download", label: "FTPS Download", description: "Download files via FTPS" },
      { value: "ftps.list", label: "FTPS List", description: "List remote directory contents" },
      { value: "ftps.mkdir", label: "FTPS Create Directory", description: "Create a remote directory" },
      { value: "ftps.rmdir", label: "FTPS Remove Directory", description: "Remove a remote directory" },
    ],
  },
  {
    label: "PGP Crypto",
    category: "pgp",
    types: [
      { value: "pgp.encrypt", label: "PGP Encrypt", description: "Encrypt files with PGP" },
      { value: "pgp.decrypt", label: "PGP Decrypt", description: "Decrypt PGP-encrypted files" },
      { value: "pgp.sign", label: "PGP Sign", description: "Sign files with a PGP key" },
      { value: "pgp.verify", label: "PGP Verify", description: "Verify PGP signatures" },
    ],
  },
  {
    label: "Azure",
    category: "azure",
    types: [
      { value: "azure_function.execute", label: "Azure Function Execute", description: "Invoke an Azure Function" },
    ],
  },
  {
    label: "Flow Control",
    category: "flow",
    types: [
      { value: "flow.foreach", label: "For Each (Loop)", description: "Iterate over a collection" },
      { value: "flow.if", label: "If (Condition)", description: "Conditional branch" },
      { value: "flow.else", label: "Else", description: "Alternative branch" },
      { value: "flow.end", label: "End Block", description: "Close a loop or condition" },
    ],
  },
];

export const STEP_OUTPUT_META: Record<string, { key: string; description: string; valueType: string; conditional?: boolean }[]> = {
  "file.copy": [{ key: "copied_file", description: "Destination file path", valueType: "string" }],
  "file.move": [{ key: "moved_file", description: "Destination file path", valueType: "string" }],
  "file.delete": [
    { key: "deleted_file", description: "Path of the deleted file", valueType: "string" },
    { key: "existed", description: "Whether the file existed", valueType: "boolean" },
  ],
  "file.zip": [
    { key: "archive_path", description: "Path to the created ZIP archive", valueType: "string" },
    { key: "split_parts", description: "List of split archive parts", valueType: "string[]", conditional: true },
    { key: "split_count", description: "Number of split parts", valueType: "number", conditional: true },
  ],
  "file.unzip": [
    { key: "extracted_directory", description: "Output directory path", valueType: "string" },
    { key: "extracted_files", description: "List of extracted file paths", valueType: "string[]", conditional: true },
  ],
  "sftp.upload": [{ key: "uploaded_file", description: "Remote file path after upload", valueType: "string" }],
  "sftp.download": [{ key: "downloaded_file", description: "Local file path after download", valueType: "string" }],
  "sftp.list": [
    { key: "file_list", description: "JSON array of remote files", valueType: "json" },
    { key: "file_count", description: "Number of files found", valueType: "number" },
  ],
  "sftp.mkdir": [{ key: "created_directory", description: "Created directory path", valueType: "string" }],
  "ftp.upload": [{ key: "uploaded_file", description: "Remote file path after upload", valueType: "string" }],
  "ftp.download": [{ key: "downloaded_file", description: "Local file path after download", valueType: "string" }],
  "ftp.list": [
    { key: "file_list", description: "JSON array of remote files", valueType: "json" },
    { key: "file_count", description: "Number of files found", valueType: "number" },
  ],
  "ftp.mkdir": [{ key: "created_directory", description: "Created directory path", valueType: "string" }],
  "ftps.upload": [{ key: "uploaded_file", description: "Remote file path after upload", valueType: "string" }],
  "ftps.download": [{ key: "downloaded_file", description: "Local file path after download", valueType: "string" }],
  "ftps.list": [
    { key: "file_list", description: "JSON array of remote files", valueType: "json" },
    { key: "file_count", description: "Number of files found", valueType: "number" },
  ],
  "ftps.mkdir": [{ key: "created_directory", description: "Created directory path", valueType: "string" }],
  "pgp.encrypt": [{ key: "encrypted_file", description: "Encrypted file path", valueType: "string" }],
  "pgp.decrypt": [{ key: "decrypted_file", description: "Decrypted file path", valueType: "string" }],
  "pgp.sign": [{ key: "signature_file", description: "Signature file path", valueType: "string" }],
  "pgp.verify": [
    { key: "verify_status", description: "Verification result status", valueType: "string" },
    { key: "is_valid", description: "Whether signature is valid", valueType: "boolean" },
  ],
  "azure_function.execute": [
    { key: "function_success", description: "Whether the function succeeded", valueType: "boolean" },
    { key: "callback_result", description: "Output payload from the Azure Function callback", valueType: "object" },
    { key: "http_status", description: "HTTP status code from the function trigger", valueType: "number" },
  ],
};

export const SYSTEM_VARIABLES: { key: string; description: string; valueType: string }[] = [
  { key: "job.workspace", description: "Execution workspace directory", valueType: "string" },
  { key: "job.execution_id", description: "Unique execution ID", valueType: "string" },
  { key: "job.name", description: "Job name", valueType: "string" },
  { key: "job.started_at", description: "Execution start time (ISO 8601)", valueType: "string" },
  { key: "job.attempt", description: "Retry attempt (0 = first run)", valueType: "string" },
];

export const RESERVED_ALIASES = new Set(["job", "loop"]);

export interface CategoryMeta {
  color: string;
  bgColor: string;
  borderColor: string;
  textColor: string;
  icon: LucideIcon;
  label: string;
}

const CATEGORY_META: Record<string, CategoryMeta> = {
  file: { color: "bg-blue-500", bgColor: "bg-blue-50 dark:bg-blue-950", borderColor: "border-l-blue-500", textColor: "text-blue-700 dark:text-blue-300", icon: File, label: "File Operations" },
  sftp: { color: "bg-emerald-500", bgColor: "bg-emerald-50 dark:bg-emerald-950", borderColor: "border-l-emerald-500", textColor: "text-emerald-700 dark:text-emerald-300", icon: Shield, label: "SFTP" },
  ftp: { color: "bg-amber-500", bgColor: "bg-amber-50 dark:bg-amber-950", borderColor: "border-l-amber-500", textColor: "text-amber-700 dark:text-amber-300", icon: Globe, label: "FTP" },
  ftps: { color: "bg-teal-500", bgColor: "bg-teal-50 dark:bg-teal-950", borderColor: "border-l-teal-500", textColor: "text-teal-700 dark:text-teal-300", icon: Lock, label: "FTPS" },
  pgp: { color: "bg-purple-500", bgColor: "bg-purple-50 dark:bg-purple-950", borderColor: "border-l-purple-500", textColor: "text-purple-700 dark:text-purple-300", icon: KeyRound, label: "PGP Crypto" },
  azure: { color: "bg-sky-500", bgColor: "bg-sky-50 dark:bg-sky-950", borderColor: "border-l-sky-500", textColor: "text-sky-700 dark:text-sky-300", icon: Cloud, label: "Azure" },
  flow: { color: "bg-rose-500", bgColor: "bg-rose-50 dark:bg-rose-950", borderColor: "border-l-rose-500", textColor: "text-rose-700 dark:text-rose-300", icon: GitBranch, label: "Flow Control" },
};

export function getStepCategory(typeKey: string): string {
  if (typeKey.startsWith("azure_function.")) return "azure";
  if (typeKey.startsWith("flow.")) return "flow";
  return typeKey.split(".")[0];
}

export function getCategoryMeta(typeKey: string): CategoryMeta {
  return CATEGORY_META[getStepCategory(typeKey)] ?? CATEGORY_META.file;
}

export function getStepTypeLabel(typeKey: string): string {
  for (const group of STEP_TYPE_GROUPS) {
    for (const t of group.types) {
      if (t.value === typeKey) return t.label;
    }
  }
  return typeKey;
}

export function computeStepDepths(steps: StepFormData[]): number[] {
  const depths: number[] = [];
  let depth = 0;
  for (const step of steps) {
    if (step.typeKey === "flow.end") {
      depth = Math.max(0, depth - 1);
    }
    if (step.typeKey === "flow.else") {
      depths.push(Math.max(0, depth - 1));
    } else {
      depths.push(depth);
    }
    if (step.typeKey === "flow.foreach" || step.typeKey === "flow.if") {
      depth++;
    }
  }
  return depths;
}

/** Convert a step name to a valid alias: "Compress Invoice Files" → "compress_invoice_files" */
export function slugifyStepName(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .replace(/^(\d)/, "_$1")
    .slice(0, 50);
}

/** Get the effective alias for a step: explicit alias > slugified name > step order */
export function getEffectiveAlias(step: StepFormData, index: number): string {
  if (step.alias) return step.alias;
  const slug = slugifyStepName(step.name);
  if (slug && RESERVED_ALIASES.has(slug)) return `${slug}_step`;
  return slug || String(index + 1);
}

export function getStepSummary(step: StepFormData): string | null {
  const config = parseStepConfig(step.configuration, step.typeKey);

  if (step.typeKey === "flow.foreach") {
    return (config.source as string) || null;
  }

  if (step.typeKey === "flow.if") {
    const left = config.left as string;
    const op = config.operator as string;
    const right = config.right as string;
    if (left && op) return right ? `${left} ${op} ${right}` : `${left} ${op}`;
    return null;
  }

  if (step.typeKey === "flow.else" || step.typeKey === "flow.end") {
    return null;
  }

  if (step.typeKey === "azure_function.execute") {
    const fn = config.functionName as string;
    return fn ? fn : null;
  }

  if (step.typeKey === "file.zip") {
    const src = config.sourcePath as string;
    const out = config.outputPath as string;
    if (src) return `${src} \u2192 ${out}`;
    return null;
  }

  if (step.typeKey === "file.unzip") {
    const archive = config.archivePath as string;
    const out = config.outputDirectory as string;
    if (archive) return `${archive} \u2192 ${out}`;
    return null;
  }

  if (step.typeKey === "file.delete") {
    const path = config.path as string;
    return path || null;
  }

  // Transfer upload/download steps
  const op = step.typeKey.split(".")[1];
  if (op === "upload" || op === "download") {
    const local = config.localPath as string;
    const remote = config.remotePath as string;
    if (local && remote) return op === "upload" ? `${local} \u2192 ${remote}` : `${remote} \u2192 ${local}`;
    return remote || local || null;
  }

  // Transfer list/mkdir/rmdir
  if (op === "list" || op === "mkdir" || op === "rmdir") {
    return (config.remotePath as string) || null;
  }

  // PGP steps
  if (step.typeKey.startsWith("pgp.")) {
    return (config.inputPath as string) || null;
  }

  // File copy/move steps
  const src = config.sourcePath as string;
  const dst = config.destinationPath as string;
  if (src) return `${src} \u2192 ${dst}`;
  return null;
}
