"use client";

import { useState } from "react";
import { useJobExecutions, useExecution } from "@/lib/hooks/use-job-executions";
import { usePauseExecution, useResumeExecution, useCancelExecution } from "@/lib/hooks/use-job-mutations";
import { StatusBadge } from "@/components/shared/status-badge";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ChevronDown, ChevronRight, Terminal, Pause, Play, XCircle } from "lucide-react";
import { AzureFunctionTraceViewer } from "@/components/azure-function-trace-viewer";
import type { JobExecutionDto, StepExecutionDto } from "@/lib/types";

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function formatDuration(ms?: number): string {
  if (!ms) return "";
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function parseOutputData(outputData?: string): Record<string, string> | null {
  if (!outputData) return null;
  try {
    return JSON.parse(outputData);
  } catch {
    return null;
  }
}

interface ExecutionTimelineProps {
  jobId: string;
  latestExecutionId?: string;
}

export function ExecutionTimeline({ jobId, latestExecutionId }: ExecutionTimelineProps) {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useJobExecutions(jobId, page, 10);
  const [expandedId, setExpandedId] = useState<string | null>(latestExecutionId ?? null);

  const executions = data?.data ?? [];
  const pagination = data?.pagination;

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Loading executions...</p>;
  }

  if (executions.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-4 text-center">
        No executions yet. Run the job to see results here.
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {executions.map((exec, i) => (
        <ExecutionRow
          key={exec.id}
          execution={exec}
          index={executions.length - i}
          expanded={expandedId === exec.id}
          onToggle={() => setExpandedId(expandedId === exec.id ? null : exec.id)}
          isLatest={i === 0}
        />
      ))}

      {pagination && pagination.totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 pt-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Previous
          </Button>
          <span className="text-sm text-muted-foreground tabular-nums">
            Page {pagination.page} of {pagination.totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= pagination.totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Next
          </Button>
        </div>
      )}
    </div>
  );
}

function ExecutionRow({
  execution,
  index,
  expanded,
  onToggle,
  isLatest,
}: {
  execution: JobExecutionDto;
  index: number;
  expanded: boolean;
  onToggle: () => void;
  isLatest: boolean;
}) {
  const isActive =
    execution.state === "queued" || execution.state === "running" || execution.state === "paused";

  const { data: liveData } = useExecution(execution.id, isActive || expanded);
  const liveExecution = liveData?.data ?? execution;

  const pauseMutation = usePauseExecution();
  const resumeMutation = useResumeExecution();
  const cancelMutation = useCancelExecution();
  const [cancelReason, setCancelReason] = useState("");
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);

  const canPause = liveExecution.state === "running";
  const canResume = liveExecution.state === "paused";
  const canCancel = ["running", "paused", "queued"].includes(liveExecution.state);

  return (
    <Card>
      <CardContent className="p-0">
        <button
          type="button"
          onClick={onToggle}
          className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-muted/50 transition-colors"
        >
          {expanded ? (
            <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
          )}
          <StatusBadge state={liveExecution.state} pulse={liveExecution.state === "running"} />
          <span className="text-sm font-medium">
            {isLatest ? "Latest" : `#${index}`}
          </span>
          <span className="ml-auto text-sm text-muted-foreground">
            {timeAgo(execution.createdAt)}
          </span>
        </button>

        {expanded && (
          <div className="border-t px-4 py-3">
            <div className="space-y-1 text-sm">
              <div className="flex justify-between text-muted-foreground">
                <span>Triggered by: {liveExecution.triggeredBy}</span>
                {liveExecution.startedAt && liveExecution.completedAt && (
                  <span>
                    Duration:{" "}
                    {formatDuration(
                      new Date(liveExecution.completedAt).getTime() -
                        new Date(liveExecution.startedAt).getTime()
                    )}
                  </span>
                )}
              </div>
              <p className="text-xs text-muted-foreground pt-2 font-mono">
                State: {liveExecution.state}
                {liveExecution.queuedAt && ` \u00b7 Queued: ${timeAgo(liveExecution.queuedAt)}`}
                {liveExecution.startedAt && ` \u00b7 Started: ${timeAgo(liveExecution.startedAt)}`}
                {liveExecution.pausedAt && ` \u00b7 Paused: ${timeAgo(liveExecution.pausedAt)}`}
                {liveExecution.cancelledAt && ` \u00b7 Cancelled: ${timeAgo(liveExecution.cancelledAt)}`}
                {liveExecution.completedAt && ` \u00b7 Completed: ${timeAgo(liveExecution.completedAt)}`}
              </p>
              {liveExecution.cancelReason && (
                <p className="text-xs text-destructive">Reason: {liveExecution.cancelReason}</p>
              )}
            </div>

            {/* Execution Control Actions */}
            {(canPause || canResume || canCancel) && (
              <div className="mt-3 flex items-center gap-2">
                {canPause && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => pauseMutation.mutate(liveExecution.id)}
                    disabled={pauseMutation.isPending}
                  >
                    <Pause className="mr-1.5 h-3.5 w-3.5" />
                    {pauseMutation.isPending ? "Pausing..." : "Pause"}
                  </Button>
                )}
                {canResume && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => resumeMutation.mutate(liveExecution.id)}
                    disabled={resumeMutation.isPending}
                  >
                    <Play className="mr-1.5 h-3.5 w-3.5" />
                    {resumeMutation.isPending ? "Resuming..." : "Resume"}
                  </Button>
                )}
                {canCancel && !showCancelConfirm && (
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={() => setShowCancelConfirm(true)}
                  >
                    <XCircle className="mr-1.5 h-3.5 w-3.5" />
                    Cancel
                  </Button>
                )}
              </div>
            )}

            {/* Cancel Confirmation */}
            {showCancelConfirm && (
              <div className="mt-3 rounded-md border border-destructive/20 bg-destructive/5 p-3 space-y-2">
                <p className="text-sm font-medium">Cancel this execution?</p>
                <input
                  type="text"
                  placeholder="Reason (optional)"
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  className="w-full rounded-md border px-3 py-1.5 text-sm bg-background"
                />
                <div className="flex gap-2">
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={() => {
                      cancelMutation.mutate(
                        { executionId: liveExecution.id, reason: cancelReason || undefined },
                        { onSuccess: () => setShowCancelConfirm(false) }
                      );
                    }}
                    disabled={cancelMutation.isPending}
                  >
                    {cancelMutation.isPending ? "Cancelling..." : "Confirm Cancel"}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      setShowCancelConfirm(false);
                      setCancelReason("");
                    }}
                  >
                    Keep Running
                  </Button>
                </div>
              </div>
            )}

            {/* Step Executions */}
            {liveExecution.stepExecutions && liveExecution.stepExecutions.length > 0 && (
              <div className="mt-4 space-y-1.5">
                <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">Steps</p>
                {liveExecution.stepExecutions.map((step) => (
                  <StepExecutionRow key={step.id} step={step} />
                ))}
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function StepExecutionRow({ step }: { step: StepExecutionDto }) {
  const [showTraces, setShowTraces] = useState(false);

  const outputData = parseOutputData(step.outputData);
  const invocationId = outputData?.invocation_id;
  const connectionId = outputData?.connection_id;
  const isAzureFunction = step.stepTypeKey === "azure_function.execute";
  const hasTraceData = isAzureFunction && invocationId && connectionId;

  return (
    <div className="rounded-md border bg-muted/30 px-3 py-2">
      <div className="flex items-center gap-2 text-sm">
        <StatusBadge state={step.state} />
        <span className="text-muted-foreground tabular-nums">Step {step.stepOrder}:</span>
        <span className="font-medium">{step.stepName}</span>
        <span className="text-xs text-muted-foreground font-mono">({step.stepTypeKey})</span>
        {step.durationMs != null && (
          <span className="text-xs text-muted-foreground ml-auto">
            {formatDuration(step.durationMs)}
          </span>
        )}
        {hasTraceData && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="ml-1 h-6 px-2 text-xs"
            onClick={() => setShowTraces(!showTraces)}
          >
            <Terminal className="mr-1 h-3 w-3" />
            {showTraces ? "Hide Logs" : "View Logs"}
          </Button>
        )}
      </div>
      {step.errorMessage && (
        <p className="mt-1 text-xs text-destructive font-mono">{step.errorMessage}</p>
      )}
      {showTraces && hasTraceData && (
        <div className="mt-2">
          <AzureFunctionTraceViewer
            connectionId={connectionId}
            invocationId={invocationId}
          />
        </div>
      )}
    </div>
  );
}
