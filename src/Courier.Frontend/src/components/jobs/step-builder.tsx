"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Plus, Pencil, X, GripVertical } from "lucide-react";
import { StepConfigForm, parseStepConfig, serializeStepConfig } from "./step-config-form";

export interface StepFormData {
  name: string;
  typeKey: string;
  configuration: string;
  timeoutSeconds: number;
}

interface StepBuilderProps {
  steps: StepFormData[];
  onChange: (steps: StepFormData[]) => void;
}

const STEP_TYPE_GROUPS = [
  {
    label: "File Operations",
    types: [
      { value: "file.copy", label: "File Copy" },
      { value: "file.move", label: "File Move" },
      { value: "file.zip", label: "File Zip (Compress)" },
      { value: "file.unzip", label: "File Unzip (Extract)" },
      { value: "file.delete", label: "File Delete" },
    ],
  },
  {
    label: "SFTP",
    types: [
      { value: "sftp.upload", label: "SFTP Upload" },
      { value: "sftp.download", label: "SFTP Download" },
      { value: "sftp.list", label: "SFTP List" },
      { value: "sftp.mkdir", label: "SFTP Create Directory" },
      { value: "sftp.rmdir", label: "SFTP Remove Directory" },
    ],
  },
  {
    label: "FTP",
    types: [
      { value: "ftp.upload", label: "FTP Upload" },
      { value: "ftp.download", label: "FTP Download" },
      { value: "ftp.list", label: "FTP List" },
      { value: "ftp.mkdir", label: "FTP Create Directory" },
      { value: "ftp.rmdir", label: "FTP Remove Directory" },
    ],
  },
  {
    label: "FTPS",
    types: [
      { value: "ftps.upload", label: "FTPS Upload" },
      { value: "ftps.download", label: "FTPS Download" },
      { value: "ftps.list", label: "FTPS List" },
      { value: "ftps.mkdir", label: "FTPS Create Directory" },
      { value: "ftps.rmdir", label: "FTPS Remove Directory" },
    ],
  },
  {
    label: "PGP Crypto",
    types: [
      { value: "pgp.encrypt", label: "PGP Encrypt" },
      { value: "pgp.decrypt", label: "PGP Decrypt" },
      { value: "pgp.sign", label: "PGP Sign" },
      { value: "pgp.verify", label: "PGP Verify" },
    ],
  },
  {
    label: "Azure",
    types: [
      { value: "azure_function.execute", label: "Azure Function Execute" },
    ],
  },
];

function getStepSummary(step: StepFormData): string | null {
  const config = parseStepConfig(step.configuration, step.typeKey);

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

export function StepBuilder({ steps, onChange }: StepBuilderProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<StepFormData>({
    name: "",
    typeKey: "file.copy",
    configuration: "{}",
    timeoutSeconds: 300,
  });

  function addStep() {
    onChange([...steps, { ...draft }]);
    setDraft({ name: "", typeKey: "file.copy", configuration: "{}", timeoutSeconds: 300 });
    setAdding(false);
  }

  function updateStep(index: number, updated: StepFormData) {
    const newSteps = [...steps];
    newSteps[index] = updated;
    onChange(newSteps);
  }

  function removeStep(index: number) {
    onChange(steps.filter((_, i) => i !== index));
    if (editingIndex === index) setEditingIndex(null);
  }

  function moveStep(from: number, to: number) {
    if (to < 0 || to >= steps.length) return;
    const newSteps = [...steps];
    const [moved] = newSteps.splice(from, 1);
    newSteps.splice(to, 0, moved);
    onChange(newSteps);
    if (editingIndex === from) setEditingIndex(to);
  }

  function handleTypeChange(step: StepFormData, index: number | null, newTypeKey: string) {
    const updated = { ...step, typeKey: newTypeKey, configuration: "{}" };
    if (index !== null) {
      updateStep(index, updated);
    } else {
      setDraft(updated);
    }
  }

  function renderTypeSelect(value: string, onChangeValue: (v: string) => void) {
    return (
      <select
        value={value}
        onChange={(e) => onChangeValue(e.target.value)}
        className="rounded-md border bg-background px-3 py-1.5 text-sm"
      >
        {STEP_TYPE_GROUPS.map((group) => (
          <optgroup key={group.label} label={group.label}>
            {group.types.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </optgroup>
        ))}
      </select>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <Label className="text-base font-medium">Steps</Label>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setAdding(true)}
          disabled={adding}
        >
          <Plus className="mr-1 h-3 w-3" />
          Add Step
        </Button>
      </div>

      {steps.length === 0 && !adding && (
        <p className="text-sm text-muted-foreground py-4 text-center">
          No steps yet. Add a step to define what this job does.
        </p>
      )}

      {steps.map((step, index) => {
        const config = parseStepConfig(step.configuration, step.typeKey);
        const isEditing = editingIndex === index;
        const summary = getStepSummary(step);

        return (
          <Card key={index}>
            <CardContent className="p-4">
              <div className="flex items-start gap-3">
                <div className="flex flex-col gap-1 pt-1">
                  <button
                    type="button"
                    onClick={() => moveStep(index, index - 1)}
                    disabled={index === 0}
                    className="text-muted-foreground hover:text-foreground disabled:opacity-30 text-xs"
                  >
                    &#9650;
                  </button>
                  <GripVertical className="h-4 w-4 text-muted-foreground" />
                  <button
                    type="button"
                    onClick={() => moveStep(index, index + 1)}
                    disabled={index === steps.length - 1}
                    className="text-muted-foreground hover:text-foreground disabled:opacity-30 text-xs"
                  >
                    &#9660;
                  </button>
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-muted-foreground tabular-nums">
                      {index + 1}.
                    </span>
                    {isEditing ? (
                      <Input
                        value={step.name}
                        onChange={(e) =>
                          updateStep(index, { ...step, name: e.target.value })
                        }
                        className="h-7 text-sm"
                      />
                    ) : (
                      <span className="font-medium">{step.name}</span>
                    )}
                    <Badge variant="secondary" className="text-xs font-mono">
                      {step.typeKey}
                    </Badge>
                  </div>

                  {isEditing ? (
                    <>
                      <div className="mt-2 grid gap-1.5">
                        <Label className="text-xs">Step Type</Label>
                        {renderTypeSelect(step.typeKey, (v) => handleTypeChange(step, index, v))}
                      </div>
                      <StepConfigForm
                        typeKey={step.typeKey}
                        config={config}
                        onChange={(c) =>
                          updateStep(index, {
                            ...step,
                            configuration: serializeStepConfig(c, step.typeKey),
                          })
                        }
                      />
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        className="mt-2"
                        onClick={() => setEditingIndex(null)}
                      >
                        Done
                      </Button>
                    </>
                  ) : (
                    summary && (
                      <p className="mt-1 text-sm text-muted-foreground font-mono">
                        {summary}
                      </p>
                    )
                  )}
                </div>

                <div className="flex gap-1">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7"
                    onClick={() =>
                      setEditingIndex(isEditing ? null : index)
                    }
                  >
                    <Pencil className="h-3 w-3" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7 text-destructive"
                    onClick={() => removeStep(index)}
                  >
                    <X className="h-3 w-3" />
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        );
      })}

      {adding && (
        <Card className="border-dashed">
          <CardContent className="p-4 space-y-3">
            <div className="grid gap-1.5">
              <Label className="text-xs">Step Name</Label>
              <Input
                placeholder="e.g., Copy invoice files"
                value={draft.name}
                onChange={(e) => setDraft({ ...draft, name: e.target.value })}
              />
            </div>
            <div className="grid gap-1.5">
              <Label className="text-xs">Step Type</Label>
              {renderTypeSelect(draft.typeKey, (v) => handleTypeChange(draft, null, v))}
            </div>
            <StepConfigForm
              typeKey={draft.typeKey}
              config={parseStepConfig(draft.configuration, draft.typeKey)}
              onChange={(c) =>
                setDraft({ ...draft, configuration: serializeStepConfig(c, draft.typeKey) })
              }
            />
            <div className="flex gap-2">
              <Button
                type="button"
                size="sm"
                onClick={addStep}
                disabled={!draft.name.trim()}
              >
                Add
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setAdding(false)}
              >
                Cancel
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
