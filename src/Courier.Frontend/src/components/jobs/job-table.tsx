"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Eye, MoreHorizontal, Pencil, Play, Trash2 } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { useDeleteJob, useTriggerJob } from "@/lib/hooks/use-job-mutations";
import { toast } from "sonner";
import { TagBadge } from "@/components/tags/tag-badge";
import type { JobDto } from "@/lib/types";

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

interface JobTableProps {
  jobs: JobDto[];
}

export function JobTable({ jobs }: JobTableProps) {
  const router = useRouter();
  const deleteJob = useDeleteJob();
  const [deleteTarget, setDeleteTarget] = useState<JobDto | null>(null);
  const [runTarget, setRunTarget] = useState<JobDto | null>(null);

  function handleDelete() {
    if (!deleteTarget) return;
    deleteJob.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success("Job deleted");
        setDeleteTarget(null);
      },
      onError: (error) => {
        toast.error(error.message);
      },
    });
  }

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Description</TableHead>
            <TableHead>Version</TableHead>
            <TableHead>Enabled</TableHead>
            <TableHead>Tags</TableHead>
            <TableHead>Created</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {jobs.map((job) => (
            <TableRow
              key={job.id}
              className="group cursor-pointer"
              onClick={() => router.push(`/jobs/${job.id}`)}
            >
              <TableCell>
                <span className="font-medium text-primary">
                  {job.name}
                </span>
              </TableCell>
              <TableCell className="text-muted-foreground max-w-[200px] truncate">
                {job.description || "\u2014"}
              </TableCell>
              <TableCell>
                <Badge variant="secondary" className="font-mono text-xs">
                  v{job.currentVersion}
                </Badge>
              </TableCell>
              <TableCell>
                <Badge variant={job.isEnabled ? "default" : "secondary"}>
                  {job.isEnabled ? "Yes" : "No"}
                </Badge>
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-1">
                  {job.tags?.map((tag) => (
                    <TagBadge key={tag.name} name={tag.name} color={tag.color} />
                  ))}
                </div>
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {timeAgo(job.createdAt)}
              </TableCell>
              <TableCell onClick={(e) => e.stopPropagation()}>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => router.push(`/jobs/${job.id}`)}>
                      <Eye className="mr-2 h-4 w-4" />
                      View
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => router.push(`/jobs/${job.id}/edit`)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => setRunTarget(job)}>
                      <Play className="mr-2 h-4 w-4" />
                      Run
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={() => setDeleteTarget(job)}
                      className="text-destructive"
                    >
                      <Trash2 className="mr-2 h-4 w-4" />
                      Delete
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <ConfirmDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete Job"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? This action cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteJob.isPending}
        onConfirm={handleDelete}
      />

      {runTarget && (
        <RunDialog job={runTarget} onClose={() => setRunTarget(null)} />
      )}
    </>
  );
}

function RunDialog({ job, onClose }: { job: JobDto; onClose: () => void }) {
  const trigger = useTriggerJob(job.id);
  const router = useRouter();

  function handleRun() {
    trigger.mutate(undefined, {
      onSuccess: () => {
        toast.success("Job queued");
        onClose();
        router.push(`/jobs/${job.id}`);
      },
      onError: (error) => {
        toast.error(error.message);
      },
    });
  }

  return (
    <ConfirmDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title="Run Job"
      description={`Run "${job.name}" now?`}
      confirmLabel="Run"
      loading={trigger.isPending}
      onConfirm={handleRun}
    />
  );
}
