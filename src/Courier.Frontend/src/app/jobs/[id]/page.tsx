"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useJob } from "@/lib/hooks/use-jobs";
import { useJobSteps } from "@/lib/hooks/use-job-steps";
import { RunButton } from "@/components/jobs/run-button";
import { ExecutionTimeline } from "@/components/jobs/execution-timeline";
import { SchedulePanel } from "@/components/jobs/schedule-panel";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Pencil } from "lucide-react";

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

function parseConfig(json: string): { sourcePath?: string; destinationPath?: string } {
  try {
    return JSON.parse(json);
  } catch {
    return {};
  }
}

export default function JobDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data: jobData, isLoading: jobLoading } = useJob(id);
  const { data: stepsData, isLoading: stepsLoading } = useJobSteps(id);
  const [latestExecutionId, setLatestExecutionId] = useState<string | undefined>();

  if (jobLoading || stepsLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  const job = jobData?.data;
  if (!job) {
    return <p className="text-muted-foreground">Job not found.</p>;
  }

  const steps = stepsData?.data ?? [];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{job.name}</h1>
          {job.description && (
            <p className="mt-1 text-muted-foreground">{job.description}</p>
          )}
          <div className="mt-3 flex items-center gap-2">
            <Badge variant="secondary" className="font-mono text-xs">
              v{job.currentVersion}
            </Badge>
            <Badge variant={job.isEnabled ? "default" : "secondary"}>
              {job.isEnabled ? "Enabled" : "Disabled"}
            </Badge>
            <span className="text-sm text-muted-foreground">
              Created {timeAgo(job.createdAt)}
            </span>
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" asChild>
            <Link href={`/jobs/${id}/edit`}>
              <Pencil className="mr-2 h-4 w-4" />
              Edit
            </Link>
          </Button>
          <RunButton
            jobId={id}
            jobName={job.name}
            onTriggered={(execId) => setLatestExecutionId(execId)}
          />
        </div>
      </div>

      <Separator />

      {/* Steps */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Steps ({steps.length})</CardTitle>
        </CardHeader>
        <CardContent>
          {steps.length === 0 ? (
            <p className="text-sm text-muted-foreground">No steps configured.</p>
          ) : (
            <div className="space-y-2">
              {steps.map((step) => {
                const config = parseConfig(step.configuration);
                return (
                  <div
                    key={step.id}
                    className="flex items-center gap-3 rounded-md border px-4 py-3"
                  >
                    <span className="text-sm font-medium text-muted-foreground tabular-nums">
                      {step.stepOrder}.
                    </span>
                    <span className="font-medium">{step.name}</span>
                    <Badge variant="secondary" className="text-xs font-mono">
                      {step.typeKey}
                    </Badge>
                    {config.sourcePath && (
                      <span className="ml-auto text-sm text-muted-foreground font-mono">
                        {config.sourcePath} &rarr; {config.destinationPath}
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Schedules */}
      <SchedulePanel jobId={id} />

      {/* Executions */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Executions</CardTitle>
        </CardHeader>
        <CardContent>
          <ExecutionTimeline jobId={id} latestExecutionId={latestExecutionId} />
        </CardContent>
      </Card>
    </div>
  );
}
