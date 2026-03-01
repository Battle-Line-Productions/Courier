"use client";

import { useState } from "react";
import { FolderOpen } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { FileBrowserDialog } from "@/components/shared/file-browser-dialog";

// --- File Zip Step Config ---

export interface FileZipStepConfig {
  sourcePath: string;
  outputPath: string;
  password: string;
}

interface FileZipStepConfigFormProps {
  config: FileZipStepConfig;
  onChange: (config: FileZipStepConfig) => void;
}

export function FileZipStepConfigForm({ config, onChange }: FileZipStepConfigFormProps) {
  const [browseTarget, setBrowseTarget] = useState<"source" | "output" | null>(null);

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">Source Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/files/report.csv"
            value={config.sourcePath}
            onChange={(e) => onChange({ ...config, sourcePath: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setBrowseTarget("source")}
            title="Browse filesystem"
          >
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <Label className="text-xs">Output Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/archives/report.zip"
            value={config.outputPath}
            onChange={(e) => onChange({ ...config, outputPath: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setBrowseTarget("output")}
            title="Browse filesystem"
          >
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <Label className="text-xs">Password (optional)</Label>
        <Input
          type="password"
          placeholder="Leave empty for no encryption"
          value={config.password}
          onChange={(e) => onChange({ ...config, password: e.target.value })}
        />
      </div>

      <FileBrowserDialog
        open={browseTarget !== null}
        onOpenChange={(open) => { if (!open) setBrowseTarget(null); }}
        onSelect={(path) => {
          if (browseTarget === "source") {
            onChange({ ...config, sourcePath: path });
          } else if (browseTarget === "output") {
            onChange({ ...config, outputPath: path });
          }
        }}
      />
    </div>
  );
}

export function parseFileZipConfig(configJson: string): FileZipStepConfig {
  try {
    const parsed = JSON.parse(configJson);
    return {
      sourcePath: parsed.source_path || "",
      outputPath: parsed.output_path || "",
      password: parsed.password || "",
    };
  } catch {
    return { sourcePath: "", outputPath: "", password: "" };
  }
}

export function serializeFileZipConfig(config: FileZipStepConfig): string {
  return JSON.stringify({
    source_path: config.sourcePath || undefined,
    output_path: config.outputPath || undefined,
    password: config.password || undefined,
  });
}

// --- File Unzip Step Config ---

export interface FileUnzipStepConfig {
  archivePath: string;
  outputDirectory: string;
  password: string;
}

interface FileUnzipStepConfigFormProps {
  config: FileUnzipStepConfig;
  onChange: (config: FileUnzipStepConfig) => void;
}

export function FileUnzipStepConfigForm({ config, onChange }: FileUnzipStepConfigFormProps) {
  const [browseTarget, setBrowseTarget] = useState<"archive" | "output" | null>(null);

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">Archive Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/archives/report.zip"
            value={config.archivePath}
            onChange={(e) => onChange({ ...config, archivePath: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setBrowseTarget("archive")}
            title="Browse filesystem"
          >
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <Label className="text-xs">Output Directory</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/extracted/"
            value={config.outputDirectory}
            onChange={(e) => onChange({ ...config, outputDirectory: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setBrowseTarget("output")}
            title="Browse filesystem"
          >
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <Label className="text-xs">Password (optional)</Label>
        <Input
          type="password"
          placeholder="Leave empty if not encrypted"
          value={config.password}
          onChange={(e) => onChange({ ...config, password: e.target.value })}
        />
      </div>

      <FileBrowserDialog
        open={browseTarget !== null}
        onOpenChange={(open) => { if (!open) setBrowseTarget(null); }}
        onSelect={(path) => {
          if (browseTarget === "archive") {
            onChange({ ...config, archivePath: path });
          } else if (browseTarget === "output") {
            onChange({ ...config, outputDirectory: path });
          }
        }}
      />
    </div>
  );
}

export function parseFileUnzipConfig(configJson: string): FileUnzipStepConfig {
  try {
    const parsed = JSON.parse(configJson);
    return {
      archivePath: parsed.archive_path || "",
      outputDirectory: parsed.output_directory || "",
      password: parsed.password || "",
    };
  } catch {
    return { archivePath: "", outputDirectory: "", password: "" };
  }
}

export function serializeFileUnzipConfig(config: FileUnzipStepConfig): string {
  return JSON.stringify({
    archive_path: config.archivePath || undefined,
    output_directory: config.outputDirectory || undefined,
    password: config.password || undefined,
  });
}

// --- File Delete Step Config ---

export interface FileDeleteStepConfig {
  path: string;
  failIfNotFound: boolean;
}

interface FileDeleteStepConfigFormProps {
  config: FileDeleteStepConfig;
  onChange: (config: FileDeleteStepConfig) => void;
}

export function FileDeleteStepConfigForm({ config, onChange }: FileDeleteStepConfigFormProps) {
  const [browsing, setBrowsing] = useState(false);

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">File Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/temp/report.csv"
            value={config.path}
            onChange={(e) => onChange({ ...config, path: e.target.value })}
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
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.failIfNotFound}
          onChange={(e) => onChange({ ...config, failIfNotFound: e.target.checked })}
          className="rounded border"
        />
        Fail if file not found
      </label>

      <FileBrowserDialog
        open={browsing}
        onOpenChange={(open) => { if (!open) setBrowsing(false); }}
        onSelect={(path) => {
          onChange({ ...config, path });
        }}
      />
    </div>
  );
}

export function parseFileDeleteConfig(configJson: string): FileDeleteStepConfig {
  try {
    const parsed = JSON.parse(configJson);
    return {
      path: parsed.path || "",
      failIfNotFound: parsed.fail_if_not_found ?? false,
    };
  } catch {
    return { path: "", failIfNotFound: false };
  }
}

export function serializeFileDeleteConfig(config: FileDeleteStepConfig): string {
  return JSON.stringify({
    path: config.path || undefined,
    fail_if_not_found: config.failIfNotFound,
  });
}
