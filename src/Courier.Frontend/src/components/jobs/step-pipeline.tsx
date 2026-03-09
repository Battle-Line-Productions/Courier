"use client";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Pencil, X, ChevronUp, ChevronDown, Layers } from "lucide-react";
import { StepConfigForm, parseStepConfig, serializeStepConfig } from "./step-config-form";
import { StepTypePicker } from "./step-type-picker";
import { getCategoryMeta, getStepSummary, getStepTypeLabel, computeStepDepths, STEP_TYPE_GROUPS, STEP_OUTPUT_META, RESERVED_ALIASES, slugifyStepName, getEffectiveAlias } from "./step-constants";
import { ContextVariablePanel } from "./context-variable-panel";
import type { StepFormData } from "./step-builder";

interface StepPipelineProps {
  steps: StepFormData[];
  editingIndex: number | null;
  onEdit: (index: number | null) => void;
  onUpdate: (index: number, step: StepFormData) => void;
  onRemove: (index: number) => void;
  onMove: (from: number, to: number) => void;
  onAddClick: () => void;
}

export function StepPipeline({
  steps,
  editingIndex,
  onEdit,
  onUpdate,
  onRemove,
  onMove,
  onAddClick,
}: StepPipelineProps) {
  const depths = computeStepDepths(steps);

  if (steps.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <div className="rounded-full bg-muted p-4 mb-4">
          <Layers className="h-8 w-8 text-muted-foreground" />
        </div>
        <h3 className="text-lg font-semibold mb-1">No steps yet</h3>
        <p className="text-sm text-muted-foreground mb-4 max-w-sm">
          No steps yet. Add a step to define what this job does.
        </p>
        <Button type="button" onClick={onAddClick}>
          Add First Step
        </Button>
      </div>
    );
  }

  return (
    <div className="relative">
      {steps.map((step, index) => {
        const meta = getCategoryMeta(step.typeKey);
        const Icon = meta.icon;
        const summary = getStepSummary(step);
        const depth = depths[index];
        const isLast = index === steps.length - 1;
        const isFlowControl = step.typeKey.startsWith("flow.");
        const isEditing = editingIndex === index;
        const config = parseStepConfig(step.configuration, step.typeKey);

        return (
          <div
            key={index}
            className="relative flex items-stretch"
            style={{ paddingLeft: `${depth * 32}px` }}
          >
            {/* Vertical connecting line */}
            {!isLast && (
              <div
                className="absolute w-px bg-border"
                style={{
                  left: `${depth * 32 + 16}px`,
                  top: "40px",
                  bottom: "0",
                }}
              />
            )}

            {/* Numbered circle */}
            <div className="relative z-10 flex flex-col items-center mr-3 pt-3">
              <div
                className={`
                  flex items-center justify-center rounded-full text-white text-xs font-bold
                  ${isFlowControl ? "h-7 w-7" : "h-8 w-8"}
                  ${meta.color}
                `}
              >
                {isFlowControl ? (
                  <Icon className="h-3.5 w-3.5" />
                ) : (
                  <span>{index + 1}</span>
                )}
              </div>
            </div>

            {/* Step card */}
            <div
              className={`
                flex-1 mb-2 rounded-lg border border-l-4 ${meta.borderColor}
                bg-card ${isEditing ? "shadow-md ring-1 ring-primary/20" : "hover:shadow-sm"} transition-shadow
              `}
            >
              <div className="flex items-center gap-2 px-3 py-2.5">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-muted-foreground tabular-nums">
                      {index + 1}.
                    </span>
                    {isEditing ? (
                      <Input
                        value={step.name}
                        onChange={(e) => onUpdate(index, { ...step, name: e.target.value })}
                        className="h-7 text-sm"
                      />
                    ) : (
                      <span className="font-medium text-sm truncate">{step.name}</span>
                    )}
                    <Badge
                      variant="secondary"
                      className={`text-xs font-mono shrink-0 ${meta.bgColor} ${meta.textColor} border-0`}
                    >
                      {step.typeKey}
                    </Badge>
                  </div>
                  {!isEditing && summary && (
                    <p className="mt-0.5 text-xs text-muted-foreground font-mono truncate pl-8">
                      {summary}
                    </p>
                  )}
                  {!isEditing && !isFlowControl && (STEP_OUTPUT_META[step.typeKey] ?? []).length > 0 && (
                    <div className="flex items-center gap-1 mt-0.5 pl-8">
                      {(STEP_OUTPUT_META[step.typeKey] ?? []).filter(o => !o.conditional).map((output) => (
                        <Badge
                          key={output.key}
                          variant="outline"
                          className="text-[10px] font-mono px-1 py-0 text-muted-foreground"
                        >
                          &rarr; {output.key}
                        </Badge>
                      ))}
                    </div>
                  )}
                </div>

                {/* Actions */}
                <div className="flex items-center gap-0.5 shrink-0">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7 text-muted-foreground hover:text-foreground"
                    onClick={() => onMove(index, index - 1)}
                    disabled={index === 0}
                  >
                    <ChevronUp className="h-3.5 w-3.5" />
                    <span className="sr-only">&#9650;</span>
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7 text-muted-foreground hover:text-foreground"
                    onClick={() => onMove(index, index + 1)}
                    disabled={isLast}
                  >
                    <ChevronDown className="h-3.5 w-3.5" />
                    <span className="sr-only">&#9660;</span>
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7"
                    onClick={() => onEdit(isEditing ? null : index)}
                  >
                    <Pencil className="h-3 w-3" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7 text-destructive"
                    onClick={() => onRemove(index)}
                  >
                    <X className="h-3 w-3" />
                  </Button>
                </div>
              </div>

              {/* Inline editing panel */}
              {isEditing && (
                <div className="px-3 pb-3 space-y-3 border-t mt-1 pt-3">
                  {/* Reference ID — auto-derived from name, editable override */}
                  {!isFlowControl && (STEP_OUTPUT_META[step.typeKey] ?? []).length > 0 && (
                    <div className="flex items-center gap-2 text-xs">
                      <span className="text-muted-foreground whitespace-nowrap">Reference ID:</span>
                      <Input
                        value={step.alias || ""}
                        placeholder={slugifyStepName(step.name) || String(index + 1)}
                        onChange={(e) => {
                          const val = e.target.value.toLowerCase().replace(/[^a-z0-9_]/g, "").slice(0, 50);
                          onUpdate(index, { ...step, alias: val || undefined });
                        }}
                        className="h-6 text-xs font-mono flex-1 max-w-[200px]"
                      />
                      <code className="text-[11px] text-muted-foreground font-mono truncate">
                        context:{getEffectiveAlias(step, index)}.<span className="text-muted-foreground/50">output</span>
                      </code>
                      {step.alias && RESERVED_ALIASES.has(step.alias) && (
                        <span className="text-[10px] text-amber-600 font-medium">Reserved</span>
                      )}
                    </div>
                  )}
                  <div className="grid gap-1.5">
                    <Label className="text-xs">Step Type</Label>
                    <StepTypePicker
                      value={step.typeKey}
                      onChange={(v) =>
                        onUpdate(index, { ...step, typeKey: v, configuration: "{}" })
                      }
                      trigger={
                        <button
                          type="button"
                          className="flex items-center gap-2 rounded-md border px-3 py-2 text-sm hover:bg-accent transition-colors w-full text-left"
                        >
                          <div className={`rounded-md p-1 ${meta.color} text-white`}>
                            <Icon className="h-3.5 w-3.5" />
                          </div>
                          <span className="font-medium">{getStepTypeLabel(step.typeKey)}</span>
                          <Badge variant="secondary" className="ml-auto text-xs font-mono">
                            {step.typeKey}
                          </Badge>
                        </button>
                      }
                    />
                    {/* Hidden native select for E2E test backward compatibility */}
                    <select
                      className="sr-only"
                      value={step.typeKey}
                      onChange={(e) =>
                        onUpdate(index, { ...step, typeKey: e.target.value, configuration: "{}" })
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
                    typeKey={step.typeKey}
                    config={config}
                    onChange={(c) =>
                      onUpdate(index, {
                        ...step,
                        configuration: serializeStepConfig(c, step.typeKey),
                      })
                    }
                  />
                  <ContextVariablePanel steps={steps} currentStepIndex={index} />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => onEdit(null)}
                  >
                    Done
                  </Button>
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
