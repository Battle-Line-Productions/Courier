"use client";

import { useState } from "react";
import { ChevronDown, ChevronRight, Copy, Check, Variable, Settings } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { STEP_OUTPUT_META, SYSTEM_VARIABLES, getStepTypeLabel, getEffectiveAlias } from "./step-constants";
import type { StepFormData } from "./step-builder";

interface ContextVariablePanelProps {
  steps: StepFormData[];
  currentStepIndex: number;
}

export function ContextVariablePanel({ steps, currentStepIndex }: ContextVariablePanelProps) {
  const [expanded, setExpanded] = useState(true);
  const [copiedKey, setCopiedKey] = useState<string | null>(null);

  // Determine if we're inside a foreach loop
  const isInsideLoop = isInForEachScope(steps, currentStepIndex);

  // Get preceding steps that produce outputs
  const precedingSteps = steps
    .slice(0, currentStepIndex)
    .map((step, index) => ({
      step,
      index,
      outputs: STEP_OUTPUT_META[step.typeKey] ?? [],
    }))
    .filter(({ outputs, step }) => outputs.length > 0 && !step.typeKey.startsWith("flow."));

  function copyToClipboard(key: string) {
    navigator.clipboard.writeText(key);
    setCopiedKey(key);
    setTimeout(() => setCopiedKey(null), 1500);
  }

  return (
    <div className="rounded-lg border bg-muted/30 p-3">
      <button
        type="button"
        onClick={() => setExpanded(!expanded)}
        className="flex items-center gap-2 w-full text-left text-sm font-medium text-muted-foreground hover:text-foreground transition-colors"
      >
        {expanded ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
        <Variable className="h-3.5 w-3.5" />
        Available Variables
        <Badge variant="secondary" className="ml-auto text-xs">
          {SYSTEM_VARIABLES.length + precedingSteps.reduce((n, s) => n + s.outputs.length, 0) + (isInsideLoop ? 2 : 0)}
        </Badge>
      </button>

      {expanded && (
        <div className="mt-3 space-y-3">
          <p className="text-[11px] text-muted-foreground">
            Click a variable to copy it, then paste into any config field above.
          </p>

          {/* System Variables */}
          <div className="space-y-1">
            <div className="text-xs font-medium text-muted-foreground flex items-center gap-1">
              <Settings className="h-3 w-3" /> System Variables
            </div>
            {SYSTEM_VARIABLES.map((v) => {
              const fullKey = `context:${v.key}`;
              return (
                <VariableRow
                  key={v.key}
                  variableKey={fullKey}
                  description={v.description}
                  copiedKey={copiedKey}
                  onCopy={copyToClipboard}
                />
              );
            })}
          </div>

          {precedingSteps.map(({ step, index, outputs }) => {
            const ref = getEffectiveAlias(step, index);
            return (
              <div key={index} className="space-y-1">
                <div className="text-xs font-medium text-muted-foreground">
                  Step {index + 1}: &quot;{step.name || "Untitled"}&quot;
                  <span className="ml-1 text-muted-foreground/70">({getStepTypeLabel(step.typeKey)})</span>
                  <Badge variant="outline" className="ml-1.5 text-[10px] font-mono px-1 py-0">
                    {ref}
                  </Badge>
                </div>
                {outputs.map((output) => {
                  const fullKey = `context:${ref}.${output.key}`;
                  return (
                    <VariableRow
                      key={output.key}
                      variableKey={fullKey}
                      description={output.description}
                      conditional={output.conditional}
                      copiedKey={copiedKey}
                      onCopy={copyToClipboard}
                    />
                  );
                })}
              </div>
            );
          })}

          {isInsideLoop && (
            <div className="space-y-1">
              <div className="text-xs font-medium text-muted-foreground flex items-center gap-1">
                <span className="text-base leading-none">&#8635;</span> Loop Variables
              </div>
              <VariableRow
                variableKey="context:loop.current_item"
                description="Current iteration item"
                copiedKey={copiedKey}
                onCopy={copyToClipboard}
              />
              <VariableRow
                variableKey="context:loop.index"
                description="Zero-based iteration index"
                copiedKey={copiedKey}
                onCopy={copyToClipboard}
              />
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function VariableRow({
  variableKey,
  description,
  conditional,
  copiedKey,
  onCopy,
}: {
  variableKey: string;
  description: string;
  conditional?: boolean;
  copiedKey: string | null;
  onCopy: (key: string) => void;
}) {
  const isCopied = copiedKey === variableKey;
  return (
    <button
      type="button"
      onClick={() => onCopy(variableKey)}
      className="flex items-center gap-2 pl-2 w-full text-left rounded hover:bg-muted/50 transition-colors py-0.5 group cursor-pointer"
    >
      <code className="text-xs font-mono text-primary/80 bg-primary/5 px-1.5 py-0.5 rounded truncate">
        {variableKey}
      </code>
      {conditional && (
        <Badge variant="outline" className="text-[10px] px-1 py-0 text-amber-600 border-amber-300">
          conditional
        </Badge>
      )}
      <span className="text-[11px] text-muted-foreground hidden sm:inline truncate max-w-[180px] flex-1">
        {description}
      </span>
      {isCopied ? (
        <span className="text-[10px] text-green-600 font-medium shrink-0">Copied!</span>
      ) : (
        <Copy className="h-3 w-3 text-muted-foreground/50 group-hover:text-muted-foreground shrink-0 transition-colors" />
      )}
    </button>
  );
}

function isInForEachScope(steps: StepFormData[], currentIndex: number): boolean {
  let depth = 0;
  for (let i = 0; i < currentIndex; i++) {
    if (steps[i].typeKey === "flow.foreach") depth++;
    if (steps[i].typeKey === "flow.end") depth = Math.max(0, depth - 1);
  }
  return depth > 0;
}
