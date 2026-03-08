"use client";

import { useState } from "react";
import { ChevronDown, ChevronRight, Copy, Check, Variable } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { STEP_OUTPUT_META, getStepTypeLabel } from "./step-constants";
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

  if (precedingSteps.length === 0 && !isInsideLoop) {
    return null;
  }

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
          {precedingSteps.reduce((n, s) => n + s.outputs.length, 0) + (isInsideLoop ? 2 : 0)}
        </Badge>
      </button>

      {expanded && (
        <div className="mt-3 space-y-3">
          {precedingSteps.map(({ step, index, outputs }) => {
            const ref = step.alias || String(index + 1);
            return (
              <div key={index} className="space-y-1">
                <div className="text-xs font-medium text-muted-foreground">
                  Step {index + 1}: &quot;{step.name || "Untitled"}&quot;
                  <span className="ml-1 text-muted-foreground/70">({getStepTypeLabel(step.typeKey)})</span>
                  {step.alias && (
                    <Badge variant="outline" className="ml-1.5 text-[10px] font-mono px-1 py-0">
                      {step.alias}
                    </Badge>
                  )}
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
    <div className="flex items-center gap-2 group pl-2">
      <code className="text-xs font-mono text-primary/80 bg-primary/5 px-1.5 py-0.5 rounded flex-1 truncate">
        {variableKey}
      </code>
      {conditional && (
        <Badge variant="outline" className="text-[10px] px-1 py-0 text-amber-600 border-amber-300">
          conditional
        </Badge>
      )}
      <span className="text-[11px] text-muted-foreground hidden sm:inline truncate max-w-[180px]">
        {description}
      </span>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="h-5 w-5 opacity-0 group-hover:opacity-100 transition-opacity shrink-0"
        onClick={() => onCopy(variableKey)}
      >
        {isCopied ? <Check className="h-3 w-3 text-green-500" /> : <Copy className="h-3 w-3" />}
      </Button>
    </div>
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
