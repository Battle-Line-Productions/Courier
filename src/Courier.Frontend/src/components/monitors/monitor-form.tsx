"use client";

import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { useCreateMonitor, useUpdateMonitor } from "@/lib/hooks/use-monitor-mutations";
import { useJobs } from "@/lib/hooks/use-jobs";
import { toast } from "sonner";
import type { MonitorDto } from "@/lib/types";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";

const monitorSchema = z.object({
  name: z.string().min(1, "Name is required").max(200, "Name must be 200 characters or less"),
  description: z.string().max(1000).optional(),
  watchType: z.enum(["local", "remote"]),
  watchPath: z.string().min(1, "Watch path is required"),
  connectionId: z.string().optional(),
  triggerFileCreated: z.boolean(),
  triggerFileModified: z.boolean(),
  triggerFileExists: z.boolean(),
  filePatterns: z.string().max(2000).optional(),
  pollingIntervalSec: z.number().int().min(30, "Minimum polling interval is 30 seconds"),
  stabilityWindowSec: z.number().int().min(0).optional(),
  batchMode: z.boolean(),
  maxConsecutiveFailures: z.number().int().min(1).max(100),
  jobIds: z.array(z.string()).min(1, "At least one job must be selected"),
});

type MonitorFormValues = z.infer<typeof monitorSchema>;

interface MonitorFormProps {
  monitor?: MonitorDto;
}

function parseWatchTarget(wt: string): { type: string; path: string; connectionId?: string } {
  try {
    return JSON.parse(wt);
  } catch {
    return { type: "local", path: "" };
  }
}

function parseTriggerEvents(flags: number) {
  return {
    fileCreated: (flags & 1) !== 0,
    fileModified: (flags & 2) !== 0,
    fileExists: (flags & 4) !== 0,
  };
}

function parseFilePatterns(patternsJson?: string): string {
  if (!patternsJson) return "";
  try {
    const arr = JSON.parse(patternsJson);
    return Array.isArray(arr) ? arr.join(", ") : "";
  } catch {
    return "";
  }
}

export function MonitorForm({ monitor }: MonitorFormProps) {
  const router = useRouter();
  const createMonitor = useCreateMonitor();
  const updateMonitor = useUpdateMonitor(monitor?.id ?? "");
  const isEditing = !!monitor;

  const wt = monitor ? parseWatchTarget(monitor.watchTarget) : null;
  const triggers = monitor ? parseTriggerEvents(monitor.triggerEvents) : null;

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<MonitorFormValues>({
    resolver: zodResolver(monitorSchema),
    defaultValues: {
      name: monitor?.name ?? "",
      description: monitor?.description ?? "",
      watchType: (wt?.type as "local" | "remote") ?? "local",
      watchPath: wt?.path ?? "",
      connectionId: wt?.connectionId ?? "",
      triggerFileCreated: triggers?.fileCreated ?? true,
      triggerFileModified: triggers?.fileModified ?? false,
      triggerFileExists: triggers?.fileExists ?? false,
      filePatterns: parseFilePatterns(monitor?.filePatterns),
      pollingIntervalSec: monitor?.pollingIntervalSec ?? 60,
      stabilityWindowSec: monitor?.stabilityWindowSec ?? 0,
      batchMode: monitor?.batchMode ?? false,
      maxConsecutiveFailures: monitor?.maxConsecutiveFailures ?? 5,
      jobIds: monitor?.bindings.map((b) => b.jobId) ?? [],
    },
  });

  const { data: jobsData } = useJobs(1, 100);
  const availableJobs = jobsData?.data ?? [];
  const selectedJobIds = watch("jobIds");

  async function onSubmit(values: MonitorFormValues) {
    let triggerEvents = 0;
    if (values.triggerFileCreated) triggerEvents |= 1;
    if (values.triggerFileModified) triggerEvents |= 2;
    if (values.triggerFileExists) triggerEvents |= 4;

    const watchTarget = JSON.stringify({
      type: values.watchType,
      path: values.watchPath,
      ...(values.connectionId ? { connectionId: values.connectionId } : {}),
    });

    const patterns = values.filePatterns
      ? values.filePatterns.split(",").map((p) => p.trim()).filter(Boolean)
      : undefined;

    const payload = {
      name: values.name,
      description: values.description || undefined,
      watchTarget,
      triggerEvents,
      filePatterns: patterns ? JSON.stringify(patterns) : undefined,
      pollingIntervalSec: values.pollingIntervalSec,
      stabilityWindowSec: values.stabilityWindowSec,
      batchMode: values.batchMode,
      maxConsecutiveFailures: values.maxConsecutiveFailures,
      jobIds: values.jobIds,
    };

    try {
      if (isEditing) {
        await updateMonitor.mutateAsync(payload);
        toast.success("Monitor updated");
        router.push(`/monitors/${monitor.id}`);
      } else {
        const result = await createMonitor.mutateAsync(payload);
        toast.success("Monitor created");
        router.push(`/monitors/${result.data?.id}`);
      }
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : "An error occurred";
      toast.error(message);
    }
  }

  function toggleJob(jobId: string) {
    const current = selectedJobIds;
    if (current.includes(jobId)) {
      setValue(
        "jobIds",
        current.filter((id) => id !== jobId),
        { shouldValidate: true }
      );
    } else {
      setValue("jobIds", [...current, jobId], { shouldValidate: true });
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" asChild>
          <Link href={isEditing ? `/monitors/${monitor.id}` : "/monitors"}>
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </Button>
        <h1 className="text-2xl font-bold tracking-tight">
          {isEditing ? "Edit Monitor" : "Create Monitor"}
        </h1>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        {/* General */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">General</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" {...register("name")} placeholder="Production file monitor" />
              {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" {...register("description")} placeholder="Optional description..." rows={2} />
            </div>
          </CardContent>
        </Card>

        {/* Watch Target */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Watch Target</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="watchPath">Directory Path</Label>
              <Input id="watchPath" {...register("watchPath")} placeholder="/data/incoming" className="font-mono" />
              {errors.watchPath && <p className="text-sm text-destructive">{errors.watchPath.message}</p>}
            </div>
            <div className="space-y-2">
              <Label htmlFor="filePatterns">File Patterns</Label>
              <Input
                id="filePatterns"
                {...register("filePatterns")}
                placeholder="*.csv, *.txt, report_*.xlsx"
                className="font-mono"
              />
              <p className="text-xs text-muted-foreground">
                Comma-separated glob patterns. Leave empty to match all files.
              </p>
            </div>
          </CardContent>
        </Card>

        {/* Trigger Events */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Trigger Events</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <Label>File Created</Label>
                <p className="text-xs text-muted-foreground">Trigger when a new file appears</p>
              </div>
              <Switch
                checked={watch("triggerFileCreated")}
                onCheckedChange={(v) => setValue("triggerFileCreated", v)}
              />
            </div>
            <div className="flex items-center justify-between">
              <div>
                <Label>File Modified</Label>
                <p className="text-xs text-muted-foreground">Trigger when an existing file is modified</p>
              </div>
              <Switch
                checked={watch("triggerFileModified")}
                onCheckedChange={(v) => setValue("triggerFileModified", v)}
              />
            </div>
            <div className="flex items-center justify-between">
              <div>
                <Label>File Exists</Label>
                <p className="text-xs text-muted-foreground">Trigger every poll if matching files exist</p>
              </div>
              <Switch
                checked={watch("triggerFileExists")}
                onCheckedChange={(v) => setValue("triggerFileExists", v)}
              />
            </div>
            {errors.triggerFileCreated && (
              <p className="text-sm text-destructive">At least one trigger event is required</p>
            )}
          </CardContent>
        </Card>

        {/* Polling Settings */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Polling Settings</CardTitle>
          </CardHeader>
          <CardContent className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="pollingIntervalSec">Polling Interval (seconds)</Label>
              <Input
                id="pollingIntervalSec"
                type="number"
                {...register("pollingIntervalSec", { valueAsNumber: true })}
              />
              {errors.pollingIntervalSec && (
                <p className="text-sm text-destructive">{errors.pollingIntervalSec.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="stabilityWindowSec">Stability Window (seconds)</Label>
              <Input
                id="stabilityWindowSec"
                type="number"
                {...register("stabilityWindowSec", { valueAsNumber: true })}
              />
              <p className="text-xs text-muted-foreground">
                Wait for file size to stabilize. 0 to disable.
              </p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="maxConsecutiveFailures">Max Consecutive Failures</Label>
              <Input
                id="maxConsecutiveFailures"
                type="number"
                {...register("maxConsecutiveFailures", { valueAsNumber: true })}
              />
            </div>
            <div className="flex items-center justify-between col-span-2">
              <div>
                <Label>Batch Mode</Label>
                <p className="text-xs text-muted-foreground">
                  All detected files trigger a single job execution instead of one per file
                </p>
              </div>
              <Switch
                checked={watch("batchMode")}
                onCheckedChange={(v) => setValue("batchMode", v)}
              />
            </div>
          </CardContent>
        </Card>

        {/* Job Bindings */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Bound Jobs</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {availableJobs.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No jobs available. <Link href="/jobs" className="text-primary hover:underline">Create a job</Link> first.
              </p>
            ) : (
              <div className="space-y-2 max-h-48 overflow-y-auto">
                {availableJobs.map((job) => (
                  <label
                    key={job.id}
                    className="flex items-center gap-3 rounded-md border p-3 cursor-pointer hover:bg-muted/50 transition-colors"
                  >
                    <input
                      type="checkbox"
                      checked={selectedJobIds.includes(job.id)}
                      onChange={() => toggleJob(job.id)}
                      className="h-4 w-4 rounded border-gray-300"
                    />
                    <span className="text-sm font-medium">{job.name}</span>
                  </label>
                ))}
              </div>
            )}
            {errors.jobIds && <p className="text-sm text-destructive">{errors.jobIds.message}</p>}
          </CardContent>
        </Card>

        {/* Actions */}
        <div className="flex items-center gap-3">
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Saving..." : isEditing ? "Update Monitor" : "Create Monitor"}
          </Button>
          <Button type="button" variant="outline" asChild>
            <Link href={isEditing ? `/monitors/${monitor.id}` : "/monitors"}>Cancel</Link>
          </Button>
        </div>
      </form>
    </div>
  );
}
