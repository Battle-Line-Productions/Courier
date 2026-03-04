"use client";

import { use } from "react";
import Link from "next/link";
import { useMonitor } from "@/lib/hooks/use-monitors";
import {
  useActivateMonitor,
  usePauseMonitor,
  useDisableMonitor,
  useDeleteMonitor,
  useAcknowledgeMonitorError,
} from "@/lib/hooks/use-monitor-mutations";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/shared/status-badge";
import { FileLogTable } from "@/components/monitors/file-log-table";
import { TagPicker } from "@/components/tags/tag-picker";
import { Pencil, Play, Pause, Ban, AlertTriangle, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";

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

function parseWatchTarget(wt: string): { type: string; path: string } {
  try {
    return JSON.parse(wt);
  } catch {
    return { type: "unknown", path: wt };
  }
}

function parseTriggerEvents(flags: number): string[] {
  const events: string[] = [];
  if (flags & 1) events.push("File Created");
  if (flags & 2) events.push("File Modified");
  if (flags & 4) events.push("File Exists");
  return events;
}

function parseFilePatterns(patternsJson?: string): string[] {
  if (!patternsJson) return [];
  try {
    const arr = JSON.parse(patternsJson);
    return Array.isArray(arr) ? arr : [];
  } catch {
    return [];
  }
}

export default function MonitorDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useMonitor(id);
  const router = useRouter();
  const activateMonitor = useActivateMonitor();
  const pauseMonitor = usePauseMonitor();
  const disableMonitor = useDisableMonitor();
  const deleteMonitor = useDeleteMonitor();
  const acknowledgeError = useAcknowledgeMonitorError();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  const monitor = data?.data;
  if (!monitor) {
    return <p className="text-muted-foreground">Monitor not found.</p>;
  }

  const wt = parseWatchTarget(monitor.watchTarget);
  const triggers = parseTriggerEvents(monitor.triggerEvents);
  const patterns = parseFilePatterns(monitor.filePatterns);

  function handleAction(action: string) {
    const actions: Record<string, { mutate: (id: string, opts: { onSuccess: () => void; onError: (e: Error) => void }) => void; label: string }> = {
      activate: { mutate: activateMonitor.mutate, label: "activated" },
      pause: { mutate: pauseMonitor.mutate, label: "paused" },
      disable: { mutate: disableMonitor.mutate, label: "disabled" },
      acknowledge: { mutate: acknowledgeError.mutate, label: "error acknowledged" },
    };
    const act = actions[action];
    if (!act) return;
    act.mutate(id, {
      onSuccess: () => toast.success(`Monitor ${act.label}`),
      onError: (error) => toast.error(error.message),
    });
  }

  function handleDelete() {
    deleteMonitor.mutate(id, {
      onSuccess: () => {
        toast.success("Monitor deleted");
        router.push("/monitors");
      },
      onError: (error) => toast.error(error.message),
    });
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{monitor.name}</h1>
          <div className="mt-3 flex items-center gap-2">
            <StatusBadge state={monitor.state} />
            <Badge variant="secondary" className="text-xs capitalize">
              {wt.type}
            </Badge>
            <span className="text-sm text-muted-foreground">
              Created {timeAgo(monitor.createdAt)}
            </span>
          </div>
          {monitor.description && (
            <p className="mt-2 text-sm text-muted-foreground">{monitor.description}</p>
          )}
        </div>
        <div className="flex items-center gap-2">
          {monitor.state !== "active" && (
            <Button variant="outline" size="sm" onClick={() => handleAction("activate")}>
              <Play className="mr-1.5 h-3.5 w-3.5" />
              Activate
            </Button>
          )}
          {monitor.state === "active" && (
            <Button variant="outline" size="sm" onClick={() => handleAction("pause")}>
              <Pause className="mr-1.5 h-3.5 w-3.5" />
              Pause
            </Button>
          )}
          {monitor.state !== "disabled" && (
            <Button variant="outline" size="sm" onClick={() => handleAction("disable")}>
              <Ban className="mr-1.5 h-3.5 w-3.5" />
              Disable
            </Button>
          )}
          {monitor.state === "error" && (
            <Button variant="outline" size="sm" onClick={() => handleAction("acknowledge")}>
              <AlertTriangle className="mr-1.5 h-3.5 w-3.5" />
              Acknowledge
            </Button>
          )}
          <Button variant="outline" size="sm" asChild>
            <Link href={`/monitors/${id}/edit`}>
              <Pencil className="mr-1.5 h-3.5 w-3.5" />
              Edit
            </Link>
          </Button>
          <Button variant="outline" size="sm" onClick={() => setShowDeleteDialog(true)} className="text-destructive hover:text-destructive">
            <Trash2 className="mr-1.5 h-3.5 w-3.5" />
            Delete
          </Button>
        </div>
      </div>

      {/* Tags */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Tags</CardTitle>
        </CardHeader>
        <CardContent>
          <TagPicker entityType="monitor" entityId={id} currentTags={monitor.tags} />
        </CardContent>
      </Card>

      <Separator />

      {/* Configuration */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Configuration</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Watch Path</dt>
              <dd className="mt-0.5 font-mono text-sm">{wt.path}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Type</dt>
              <dd className="mt-0.5 text-sm capitalize">{wt.type}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Polling Interval</dt>
              <dd className="mt-0.5 text-sm">{monitor.pollingIntervalSec}s</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Trigger Events</dt>
              <dd className="mt-0.5 text-sm">{triggers.join(", ") || "None"}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Batch Mode</dt>
              <dd className="mt-0.5 text-sm">{monitor.batchMode ? "Enabled" : "Disabled"}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Stability Window</dt>
              <dd className="mt-0.5 text-sm">{monitor.stabilityWindowSec}s</dd>
            </div>
            {patterns.length > 0 && (
              <div className="col-span-full">
                <dt className="text-sm font-medium text-muted-foreground">File Patterns</dt>
                <dd className="mt-0.5 flex gap-1.5 flex-wrap">
                  {patterns.map((p, i) => (
                    <Badge key={i} variant="secondary" className="font-mono text-xs">
                      {p}
                    </Badge>
                  ))}
                </dd>
              </div>
            )}
          </dl>
        </CardContent>
      </Card>

      {/* Health */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Health</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-4">
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Last Polled</dt>
              <dd className="mt-0.5 text-sm">
                {monitor.lastPolledAt ? timeAgo(monitor.lastPolledAt) : "Never"}
              </dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Consecutive Failures</dt>
              <dd className="mt-0.5 text-sm">{monitor.consecutiveFailureCount}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Max Failures</dt>
              <dd className="mt-0.5 text-sm">{monitor.maxConsecutiveFailures}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Updated</dt>
              <dd className="mt-0.5 text-sm">{timeAgo(monitor.updatedAt)}</dd>
            </div>
          </dl>
        </CardContent>
      </Card>

      {/* Bound Jobs */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Bound Jobs ({monitor.bindings.length})</CardTitle>
        </CardHeader>
        <CardContent>
          {monitor.bindings.length === 0 ? (
            <p className="text-sm text-muted-foreground">No jobs bound to this monitor.</p>
          ) : (
            <div className="space-y-2">
              {monitor.bindings.map((binding) => (
                <div key={binding.id} className="flex items-center justify-between rounded-md border p-3">
                  <Link
                    href={`/jobs/${binding.jobId}`}
                    className="text-sm font-medium text-primary hover:underline underline-offset-4"
                  >
                    {binding.jobName || binding.jobId}
                  </Link>
                  <Badge variant="secondary" className="font-mono text-xs">
                    {binding.jobId.slice(0, 8)}...
                  </Badge>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* File Log */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">File Log</CardTitle>
        </CardHeader>
        <CardContent>
          <FileLogTable monitorId={id} />
        </CardContent>
      </Card>

      <ConfirmDialog
        open={showDeleteDialog}
        onOpenChange={setShowDeleteDialog}
        title="Delete Monitor"
        description={`Are you sure you want to delete "${monitor.name}"? This action cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteMonitor.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
