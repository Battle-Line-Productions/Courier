"use client";

import { useState } from "react";
import { FolderOpen } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { FileBrowserDialog } from "@/components/shared/file-browser-dialog";
import {
  AzureFunctionStepConfigForm,
  parseAzureFunctionConfig,
  serializeAzureFunctionConfig,
} from "./azure-function-step-config";
import type { AzureFunctionStepConfig } from "./azure-function-step-config";

// --- File step config (file.copy, file.move) ---

interface FileStepConfig {
  sourcePath: string;
  destinationPath: string;
  overwrite: boolean;
}

function FileStepConfigForm({
  config,
  onChange,
}: {
  config: FileStepConfig;
  onChange: (config: FileStepConfig) => void;
}) {
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

// --- Generic dispatcher ---

interface StepConfigFormProps {
  typeKey: string;
  config: StepConfig;
  onChange: (config: StepConfig) => void;
}

// Union config type used externally
export interface StepConfig {
  // File step fields
  sourcePath?: string;
  destinationPath?: string;
  overwrite?: boolean;
  // Azure function fields
  connectionId?: string;
  functionName?: string;
  inputPayload?: string;
  pollIntervalSec?: number;
  maxWaitSec?: number;
  initialDelaySec?: number;
  // Raw JSON passthrough for unknown types
  [key: string]: unknown;
}

export function StepConfigForm({ typeKey, config, onChange }: StepConfigFormProps) {
  if (typeKey === "azure_function.execute") {
    const azConfig: AzureFunctionStepConfig = {
      connectionId: (config.connectionId as string) ?? "",
      functionName: (config.functionName as string) ?? "",
      inputPayload: (config.inputPayload as string) ?? "",
      pollIntervalSec: (config.pollIntervalSec as number) ?? 15,
      maxWaitSec: (config.maxWaitSec as number) ?? 3600,
      initialDelaySec: (config.initialDelaySec as number) ?? 30,
    };
    return (
      <AzureFunctionStepConfigForm
        config={azConfig}
        onChange={(c) =>
          onChange({
            connectionId: c.connectionId,
            functionName: c.functionName,
            inputPayload: c.inputPayload,
            pollIntervalSec: c.pollIntervalSec,
            maxWaitSec: c.maxWaitSec,
            initialDelaySec: c.initialDelaySec,
          })
        }
      />
    );
  }

  // Default: file step config (file.copy, file.move)
  const fileConfig: FileStepConfig = {
    sourcePath: (config.sourcePath as string) ?? "",
    destinationPath: (config.destinationPath as string) ?? "",
    overwrite: (config.overwrite as boolean) ?? false,
  };
  return (
    <FileStepConfigForm
      config={fileConfig}
      onChange={(c) =>
        onChange({
          sourcePath: c.sourcePath,
          destinationPath: c.destinationPath,
          overwrite: c.overwrite,
        })
      }
    />
  );
}

export function parseStepConfig(configJson: string, typeKey?: string): StepConfig {
  if (typeKey === "azure_function.execute") {
    const az = parseAzureFunctionConfig(configJson);
    return {
      connectionId: az.connectionId,
      functionName: az.functionName,
      inputPayload: az.inputPayload,
      pollIntervalSec: az.pollIntervalSec,
      maxWaitSec: az.maxWaitSec,
      initialDelaySec: az.initialDelaySec,
    };
  }

  // Default: file step
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

export function serializeStepConfig(config: StepConfig, typeKey?: string): string {
  if (typeKey === "azure_function.execute") {
    return serializeAzureFunctionConfig({
      connectionId: (config.connectionId as string) ?? "",
      functionName: (config.functionName as string) ?? "",
      inputPayload: (config.inputPayload as string) ?? "",
      pollIntervalSec: (config.pollIntervalSec as number) ?? 15,
      maxWaitSec: (config.maxWaitSec as number) ?? 3600,
      initialDelaySec: (config.initialDelaySec as number) ?? 30,
    });
  }

  // Default: file step
  return JSON.stringify({
    sourcePath: config.sourcePath,
    destinationPath: config.destinationPath,
    overwrite: config.overwrite,
  });
}
