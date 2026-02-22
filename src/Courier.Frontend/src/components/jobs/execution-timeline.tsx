"use client";

import { useState } from "react";
import { useJobExecutions, useExecution } from "@/lib/hooks/use-job-executions";
import { StatusBadge } from "@/components/shared/status-badge";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ChevronDown, ChevronRight } from "lucide-react";
import type { JobExecutionDto } from "@/lib/types";

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
  const isRunning =
    execution.state === "queued" || execution.state === "running";

  const { data: liveData } = useExecution(execution.id, isRunning);
  const liveExecution = liveData?.data ?? execution;

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
          <StatusBadge state={liveExecution.state} pulse={isRunning} />
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
                {liveExecution.completedAt && ` \u00b7 Completed: ${timeAgo(liveExecution.completedAt)}`}
              </p>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
