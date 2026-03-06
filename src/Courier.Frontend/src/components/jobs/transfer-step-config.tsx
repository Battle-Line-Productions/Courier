"use client";

import { useState } from "react";
import { FolderOpen, HelpCircle } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { FileBrowserDialog } from "@/components/shared/file-browser-dialog";
import { useConnections } from "@/lib/hooks/use-connections";

function FieldTooltip({ text }: { text: string }) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <HelpCircle className="h-3.5 w-3.5 text-muted-foreground cursor-help" />
      </TooltipTrigger>
      <TooltipContent side="top" className="max-w-xs">
        <p className="text-xs">{text}</p>
      </TooltipContent>
    </Tooltip>
  );
}

function ConnectionPicker({
  protocol,
  value,
  onChange,
}: {
  protocol: string;
  value: string;
  onChange: (id: string) => void;
}) {
  const { data: connectionsData } = useConnections(1, 100, { protocol });
  const connections = connectionsData?.data ?? [];

  return (
    <div className="grid gap-1.5">
      <div className="flex items-center gap-1.5">
        <Label className="text-xs">Connection</Label>
        <FieldTooltip text={`Select a ${protocol.toUpperCase()} connection for this step.`} />
      </div>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger>
          <SelectValue placeholder="Select connection" />
        </SelectTrigger>
        <SelectContent>
          {connections.map((conn) => (
            <SelectItem key={conn.id} value={conn.id}>
              {conn.name} ({conn.host})
            </SelectItem>
          ))}
          {connections.length === 0 && (
            <SelectItem value="__none__" disabled>
              No {protocol.toUpperCase()} connections found
            </SelectItem>
          )}
        </SelectContent>
      </Select>
    </div>
  );
}

/** Extracts protocol from typeKey: "sftp.upload" → "sftp" */
function protocolFromTypeKey(typeKey: string): string {
  return typeKey.split(".")[0];
}

// --- Upload ---

export interface TransferUploadConfig {
  connectionId: string;
  localPath: string;
  remotePath: string;
  atomicUpload: boolean;
  atomicSuffix: string;
  resumePartial: boolean;
}

export function TransferUploadForm({
  typeKey,
  config,
  onChange,
}: {
  typeKey: string;
  config: TransferUploadConfig;
  onChange: (config: TransferUploadConfig) => void;
}) {
  const [browsing, setBrowsing] = useState(false);
  const protocol = protocolFromTypeKey(typeKey);

  return (
    <div className="grid gap-3 pt-2">
      <ConnectionPicker
        protocol={protocol}
        value={config.connectionId}
        onChange={(v) => onChange({ ...config, connectionId: v })}
      />
      <div className="grid gap-1.5">
        <Label className="text-xs">Local Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/outgoing/report.csv"
            value={config.localPath}
            onChange={(e) => onChange({ ...config, localPath: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setBrowsing(true)}
            title="Browse filesystem"
          >
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <Label className="text-xs">Remote Path</Label>
        <Input
          placeholder="/uploads/report.csv"
          value={config.remotePath}
          onChange={(e) => onChange({ ...config, remotePath: e.target.value })}
        />
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.atomicUpload}
          onChange={(e) => onChange({ ...config, atomicUpload: e.target.checked })}
          className="rounded border"
        />
        Atomic upload (write to temp file then rename)
      </label>
      {config.atomicUpload && (
        <div className="grid gap-1.5">
          <div className="flex items-center gap-1.5">
            <Label className="text-xs">Atomic Suffix</Label>
            <FieldTooltip text="Temporary suffix appended during upload, removed on completion." />
          </div>
          <Input
            placeholder=".tmp"
            value={config.atomicSuffix}
            onChange={(e) => onChange({ ...config, atomicSuffix: e.target.value })}
          />
        </div>
      )}
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.resumePartial}
          onChange={(e) => onChange({ ...config, resumePartial: e.target.checked })}
          className="rounded border"
        />
        Resume partial uploads
      </label>

      <FileBrowserDialog
        open={browsing}
        onOpenChange={setBrowsing}
        onSelect={(path) => onChange({ ...config, localPath: path })}
      />
    </div>
  );
}

export function parseTransferUploadConfig(configJson: string): TransferUploadConfig {
  try {
    const p = JSON.parse(configJson);
    return {
      connectionId: p.connection_id || "",
      localPath: p.local_path || "",
      remotePath: p.remote_path || "",
      atomicUpload: p.atomic_upload ?? false,
      atomicSuffix: p.atomic_suffix || ".tmp",
      resumePartial: p.resume_partial ?? false,
    };
  } catch {
    return { connectionId: "", localPath: "", remotePath: "", atomicUpload: false, atomicSuffix: ".tmp", resumePartial: false };
  }
}

export function serializeTransferUploadConfig(config: TransferUploadConfig): string {
  return JSON.stringify({
    connection_id: config.connectionId || undefined,
    local_path: config.localPath || undefined,
    remote_path: config.remotePath || undefined,
    atomic_upload: config.atomicUpload || undefined,
    atomic_suffix: config.atomicUpload ? config.atomicSuffix || undefined : undefined,
    resume_partial: config.resumePartial || undefined,
  });
}

// --- Download ---

export interface TransferDownloadConfig {
  connectionId: string;
  remotePath: string;
  localPath: string;
  filePattern: string;
  resumePartial: boolean;
  deleteAfterDownload: boolean;
}

export function TransferDownloadForm({
  typeKey,
  config,
  onChange,
}: {
  typeKey: string;
  config: TransferDownloadConfig;
  onChange: (config: TransferDownloadConfig) => void;
}) {
  const [browsing, setBrowsing] = useState(false);
  const protocol = protocolFromTypeKey(typeKey);

  return (
    <div className="grid gap-3 pt-2">
      <ConnectionPicker
        protocol={protocol}
        value={config.connectionId}
        onChange={(v) => onChange({ ...config, connectionId: v })}
      />
      <div className="grid gap-1.5">
        <Label className="text-xs">Remote Path</Label>
        <Input
          placeholder="/incoming/data/"
          value={config.remotePath}
          onChange={(e) => onChange({ ...config, remotePath: e.target.value })}
        />
      </div>
      <div className="grid gap-1.5">
        <Label className="text-xs">Local Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/downloads/"
            value={config.localPath}
            onChange={(e) => onChange({ ...config, localPath: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setBrowsing(true)}
            title="Browse filesystem"
          >
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">File Pattern</Label>
          <FieldTooltip text="Optional glob pattern to filter files (e.g., *.csv)." />
        </div>
        <Input
          placeholder="*.csv"
          value={config.filePattern}
          onChange={(e) => onChange({ ...config, filePattern: e.target.value })}
        />
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.resumePartial}
          onChange={(e) => onChange({ ...config, resumePartial: e.target.checked })}
          className="rounded border"
        />
        Resume partial downloads
      </label>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.deleteAfterDownload}
          onChange={(e) => onChange({ ...config, deleteAfterDownload: e.target.checked })}
          className="rounded border"
        />
        Delete remote file after download
      </label>

      <FileBrowserDialog
        open={browsing}
        onOpenChange={setBrowsing}
        onSelect={(path) => onChange({ ...config, localPath: path })}
      />
    </div>
  );
}

export function parseTransferDownloadConfig(configJson: string): TransferDownloadConfig {
  try {
    const p = JSON.parse(configJson);
    return {
      connectionId: p.connection_id || "",
      remotePath: p.remote_path || "",
      localPath: p.local_path || "",
      filePattern: p.file_pattern || "",
      resumePartial: p.resume_partial ?? false,
      deleteAfterDownload: p.delete_after_download ?? false,
    };
  } catch {
    return { connectionId: "", remotePath: "", localPath: "", filePattern: "", resumePartial: false, deleteAfterDownload: false };
  }
}

export function serializeTransferDownloadConfig(config: TransferDownloadConfig): string {
  return JSON.stringify({
    connection_id: config.connectionId || undefined,
    remote_path: config.remotePath || undefined,
    local_path: config.localPath || undefined,
    file_pattern: config.filePattern || undefined,
    resume_partial: config.resumePartial || undefined,
    delete_after_download: config.deleteAfterDownload || undefined,
  });
}

// --- List ---

export interface TransferListConfig {
  connectionId: string;
  remotePath: string;
  filePattern: string;
}

export function TransferListForm({
  typeKey,
  config,
  onChange,
}: {
  typeKey: string;
  config: TransferListConfig;
  onChange: (config: TransferListConfig) => void;
}) {
  const protocol = protocolFromTypeKey(typeKey);

  return (
    <div className="grid gap-3 pt-2">
      <ConnectionPicker
        protocol={protocol}
        value={config.connectionId}
        onChange={(v) => onChange({ ...config, connectionId: v })}
      />
      <div className="grid gap-1.5">
        <Label className="text-xs">Remote Path</Label>
        <Input
          placeholder="/incoming/"
          value={config.remotePath}
          onChange={(e) => onChange({ ...config, remotePath: e.target.value })}
        />
      </div>
      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">File Pattern</Label>
          <FieldTooltip text="Optional glob pattern to filter listing (e.g., *.xml)." />
        </div>
        <Input
          placeholder="*"
          value={config.filePattern}
          onChange={(e) => onChange({ ...config, filePattern: e.target.value })}
        />
      </div>
    </div>
  );
}

export function parseTransferListConfig(configJson: string): TransferListConfig {
  try {
    const p = JSON.parse(configJson);
    return {
      connectionId: p.connection_id || "",
      remotePath: p.remote_path || "",
      filePattern: p.file_pattern || "",
    };
  } catch {
    return { connectionId: "", remotePath: "", filePattern: "" };
  }
}

export function serializeTransferListConfig(config: TransferListConfig): string {
  return JSON.stringify({
    connection_id: config.connectionId || undefined,
    remote_path: config.remotePath || undefined,
    file_pattern: config.filePattern || undefined,
  });
}

// --- Mkdir ---

export interface TransferMkdirConfig {
  connectionId: string;
  remotePath: string;
}

export function TransferMkdirForm({
  typeKey,
  config,
  onChange,
}: {
  typeKey: string;
  config: TransferMkdirConfig;
  onChange: (config: TransferMkdirConfig) => void;
}) {
  const protocol = protocolFromTypeKey(typeKey);

  return (
    <div className="grid gap-3 pt-2">
      <ConnectionPicker
        protocol={protocol}
        value={config.connectionId}
        onChange={(v) => onChange({ ...config, connectionId: v })}
      />
      <div className="grid gap-1.5">
        <Label className="text-xs">Remote Path</Label>
        <Input
          placeholder="/outgoing/2026-02/"
          value={config.remotePath}
          onChange={(e) => onChange({ ...config, remotePath: e.target.value })}
        />
      </div>
    </div>
  );
}

export function parseTransferMkdirConfig(configJson: string): TransferMkdirConfig {
  try {
    const p = JSON.parse(configJson);
    return { connectionId: p.connection_id || "", remotePath: p.remote_path || "" };
  } catch {
    return { connectionId: "", remotePath: "" };
  }
}

export function serializeTransferMkdirConfig(config: TransferMkdirConfig): string {
  return JSON.stringify({
    connection_id: config.connectionId || undefined,
    remote_path: config.remotePath || undefined,
  });
}

// --- Rmdir ---

export interface TransferRmdirConfig {
  connectionId: string;
  remotePath: string;
  recursive: boolean;
}

export function TransferRmdirForm({
  typeKey,
  config,
  onChange,
}: {
  typeKey: string;
  config: TransferRmdirConfig;
  onChange: (config: TransferRmdirConfig) => void;
}) {
  const protocol = protocolFromTypeKey(typeKey);

  return (
    <div className="grid gap-3 pt-2">
      <ConnectionPicker
        protocol={protocol}
        value={config.connectionId}
        onChange={(v) => onChange({ ...config, connectionId: v })}
      />
      <div className="grid gap-1.5">
        <Label className="text-xs">Remote Path</Label>
        <Input
          placeholder="/outgoing/old/"
          value={config.remotePath}
          onChange={(e) => onChange({ ...config, remotePath: e.target.value })}
        />
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.recursive}
          onChange={(e) => onChange({ ...config, recursive: e.target.checked })}
          className="rounded border"
        />
        Delete recursively (including contents)
      </label>
    </div>
  );
}

export function parseTransferRmdirConfig(configJson: string): TransferRmdirConfig {
  try {
    const p = JSON.parse(configJson);
    return { connectionId: p.connection_id || "", remotePath: p.remote_path || "", recursive: p.recursive ?? false };
  } catch {
    return { connectionId: "", remotePath: "", recursive: false };
  }
}

export function serializeTransferRmdirConfig(config: TransferRmdirConfig): string {
  return JSON.stringify({
    connection_id: config.connectionId || undefined,
    remote_path: config.remotePath || undefined,
    recursive: config.recursive || undefined,
  });
}
