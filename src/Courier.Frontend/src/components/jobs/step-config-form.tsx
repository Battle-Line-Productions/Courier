"use client";

import { useState } from "react";
import { FolderOpen } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { FileBrowserDialog } from "@/components/shared/file-browser-dialog";

interface StepConfig {
  sourcePath: string;
  destinationPath: string;
  overwrite: boolean;
}

interface StepConfigFormProps {
  typeKey: string;
  config: StepConfig;
  onChange: (config: StepConfig) => void;
}

export function StepConfigForm({ typeKey, config, onChange }: StepConfigFormProps) {
  const [browseTarget, setBrowseTarget] = useState<"source" | "destination" | null>(null);

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">Source Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/incoming/"
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
        <Label className="text-xs">Destination Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/processed/"
            value={config.destinationPath}
            onChange={(e) => onChange({ ...config, destinationPath: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setBrowseTarget("destination")}
            title="Browse filesystem"
          >
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.overwrite}
          onChange={(e) => onChange({ ...config, overwrite: e.target.checked })}
          className="rounded border"
        />
        Overwrite existing files
      </label>

      <FileBrowserDialog
        open={browseTarget !== null}
        onOpenChange={(open) => { if (!open) setBrowseTarget(null); }}
        onSelect={(path) => {
          if (browseTarget === "source") {
            onChange({ ...config, sourcePath: path });
          } else if (browseTarget === "destination") {
            onChange({ ...config, destinationPath: path });
          }
        }}
      />
    </div>
  );
}

export function parseStepConfig(configJson: string): StepConfig {
  try {
    const parsed = JSON.parse(configJson);
    return {
      sourcePath: parsed.sourcePath || "",
      destinationPath: parsed.destinationPath || "",
      overwrite: parsed.overwrite || false,
    };
  } catch {
    return { sourcePath: "", destinationPath: "", overwrite: false };
  }
}

export function serializeStepConfig(config: StepConfig): string {
  return JSON.stringify(config);
}
