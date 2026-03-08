"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Plus } from "lucide-react";
import { StepConfigForm, parseStepConfig, serializeStepConfig } from "./step-config-form";
import { StepTypePicker } from "./step-type-picker";
import { StepPipeline } from "./step-pipeline";
import { getCategoryMeta, getStepTypeLabel, STEP_TYPE_GROUPS } from "./step-constants";

export interface StepFormData {
  name: string;
  typeKey: string;
  configuration: string;
  timeoutSeconds: number;
  alias?: string;
}

interface StepBuilderProps {
  steps: StepFormData[];
  onChange: (steps: StepFormData[]) => void;
}

export function StepBuilder({ steps, onChange }: StepBuilderProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<StepFormData>({
    name: "",
    typeKey: "file.copy",
    configuration: "{}",
    timeoutSeconds: 300,
    alias: "",
  });

  function addStep() {
    onChange([...steps, { ...draft }]);
    setDraft({ name: "", typeKey: "file.copy", configuration: "{}", timeoutSeconds: 300, alias: "" });
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

    // Build old→new order mapping (1-based step orders)
    const rewriteMap = new Map<number, number>();
    // After splice, the positions shifted. Track which original index is now at which position.
    const originalIndices = steps.map((_, i) => i);
    const reordered = [...originalIndices];
    const [movedIdx] = reordered.splice(from, 1);
    reordered.splice(to, 0, movedIdx);
    for (let i = 0; i < reordered.length; i++) {
      const oldOrder = reordered[i] + 1; // 1-based
      const newOrder = i + 1;
      if (oldOrder !== newOrder) {
        rewriteMap.set(oldOrder, newOrder);
      }
    }

    // Rewrite context references in all step configurations
    if (rewriteMap.size > 0) {
      for (let i = 0; i < newSteps.length; i++) {
        let config = newSteps[i].configuration;
        for (const [oldOrder, newOrder] of rewriteMap) {
          const pattern = new RegExp(`context:${oldOrder}\\.`, "g");
          config = config.replace(pattern, `context:${newOrder}.`);
        }
        if (config !== newSteps[i].configuration) {
          newSteps[i] = { ...newSteps[i], configuration: config };
        }
      }
    }

    onChange(newSteps);
    if (editingIndex === from) setEditingIndex(to);
  }

  const draftMeta = getCategoryMeta(draft.typeKey);
  const DraftIcon = draftMeta.icon;

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

      <StepPipeline
        steps={steps}
        editingIndex={editingIndex}
        onEdit={setEditingIndex}
        onUpdate={updateStep}
        onRemove={removeStep}
        onMove={moveStep}
        onAddClick={() => setAdding(true)}
      />

      {adding && (
        <div className="rounded-lg border-2 border-dashed border-primary/30 bg-card p-4 space-y-3">
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
            <StepTypePicker
              value={draft.typeKey}
              onChange={(v) => setDraft({ ...draft, typeKey: v, configuration: "{}" })}
              trigger={
                <button
                  type="button"
                  className="flex items-center gap-2 rounded-md border px-3 py-2 text-sm hover:bg-accent transition-colors w-full text-left"
                >
                  <div className={`rounded-md p-1 ${draftMeta.color} text-white`}>
                    <DraftIcon className="h-3.5 w-3.5" />
                  </div>
                  <span className="font-medium">{getStepTypeLabel(draft.typeKey)}</span>
                  <Badge variant="secondary" className="ml-auto text-xs font-mono">
                    {draft.typeKey}
                  </Badge>
                </button>
              }
            />
            {/* Hidden native select for E2E test backward compatibility */}
            <select
              className="sr-only"
              value={draft.typeKey}
              onChange={(e) =>
                setDraft({ ...draft, typeKey: e.target.value, configuration: "{}" })
              }
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
        </div>
      )}
    </div>
  );
}
