"use client";

import { useState } from "react";
import { useAzureFunctionTraces } from "@/lib/hooks/use-azure-function-traces";
import { Button } from "@/components/ui/button";

interface AzureFunctionTraceViewerProps {
  connectionId: string;
  invocationId: string;
}

const severityColors: Record<number, string> = {
  0: "text-muted-foreground", // Verbose
  1: "text-foreground",       // Information
  2: "text-yellow-600",       // Warning
  3: "text-red-600",          // Error
  4: "text-red-800",          // Critical
};

const severityLabels: Record<number, string> = {
  0: "VERBOSE",
  1: "INFO",
  2: "WARN",
  3: "ERROR",
  4: "CRITICAL",
};

export function AzureFunctionTraceViewer({ connectionId, invocationId }: AzureFunctionTraceViewerProps) {
  const [expanded, setExpanded] = useState(false);
  const { data, isLoading, error } = useAzureFunctionTraces(
    expanded ? connectionId : "",
    expanded ? invocationId : ""
  );

  if (!expanded) {
    return (
      <Button variant="outline" size="sm" onClick={() => setExpanded(true)}>
        View Function Logs
      </Button>
    );
  }

  const traces = data?.data ?? [];

  return (
    <div className="rounded-md border bg-muted/30">
      <div className="flex items-center justify-between border-b px-3 py-2">
        <span className="text-sm font-medium">Function Logs</span>
        <Button variant="ghost" size="sm" onClick={() => setExpanded(false)}>
          Collapse
        </Button>
      </div>
      <div className="max-h-96 overflow-auto p-3">
        {isLoading && <p className="text-sm text-muted-foreground">Loading traces...</p>}
        {error && <p className="text-sm text-red-600">Failed to load traces.</p>}
        {!isLoading && traces.length === 0 && (
          <p className="text-sm text-muted-foreground">No trace logs found for this invocation.</p>
        )}
        {traces.map((trace, i) => (
          <div key={i} className="flex gap-2 font-mono text-xs leading-relaxed">
            <span className="shrink-0 text-muted-foreground">
              {new Date(trace.timestamp).toLocaleTimeString()}
            </span>
            <span className={`shrink-0 w-16 ${severityColors[trace.severityLevel] ?? "text-foreground"}`}>
              [{severityLabels[trace.severityLevel] ?? "UNKNOWN"}]
            </span>
            <span className={severityColors[trace.severityLevel] ?? "text-foreground"}>
              {trace.message}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
