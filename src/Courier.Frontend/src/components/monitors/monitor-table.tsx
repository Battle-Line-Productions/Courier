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
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { MoreHorizontal, Pencil, Trash2, Play, Pause, Ban, AlertTriangle } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { StatusBadge } from "@/components/shared/status-badge";
import {
  useDeleteMonitor,
  useActivateMonitor,
  usePauseMonitor,
  useDisableMonitor,
  useAcknowledgeMonitorError,
} from "@/lib/hooks/use-monitor-mutations";
import { toast } from "sonner";
import { TagBadge } from "@/components/tags/tag-badge";
import type { MonitorDto } from "@/lib/types";
import Link from "next/link";

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

function parseWatchPath(watchTarget: string): string {
  try {
    const parsed = JSON.parse(watchTarget);
    return parsed.path || "—";
  } catch {
    return "—";
  }
}

interface MonitorTableProps {
  monitors: MonitorDto[];
}

export function MonitorTable({ monitors }: MonitorTableProps) {
  const router = useRouter();
  const deleteMonitor = useDeleteMonitor();
  const activateMonitor = useActivateMonitor();
  const pauseMonitor = usePauseMonitor();
  const disableMonitor = useDisableMonitor();
  const acknowledgeError = useAcknowledgeMonitorError();
  const [deleteTarget, setDeleteTarget] = useState<MonitorDto | null>(null);

  function handleDelete() {
    if (!deleteTarget) return;
    deleteMonitor.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success("Monitor deleted");
        setDeleteTarget(null);
      },
      onError: (error) => {
        toast.error(error.message);
      },
    });
  }

  function handleStateAction(id: string, action: string) {
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

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Watch Path</TableHead>
            <TableHead>State</TableHead>
            <TableHead>Interval</TableHead>
            <TableHead>Bound Jobs</TableHead>
            <TableHead>Tags</TableHead>
            <TableHead>Last Polled</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {monitors.map((monitor) => (
            <TableRow key={monitor.id} className="group">
              <TableCell>
                <Link
                  href={`/monitors/${monitor.id}`}
                  className="font-medium text-primary hover:underline underline-offset-4"
                >
                  {monitor.name}
                </Link>
              </TableCell>
              <TableCell className="text-muted-foreground font-mono text-sm max-w-48 truncate">
                {parseWatchPath(monitor.watchTarget)}
              </TableCell>
              <TableCell>
                <StatusBadge state={monitor.state} />
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {monitor.pollingIntervalSec}s
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {monitor.bindings.length}
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-1">
                  {monitor.tags?.map((tag) => (
                    <TagBadge key={tag.name} name={tag.name} color={tag.color} />
                  ))}
                </div>
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {monitor.lastPolledAt ? timeAgo(monitor.lastPolledAt) : "Never"}
              </TableCell>
              <TableCell>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => router.push(`/monitors/${monitor.id}/edit`)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    {monitor.state !== "active" && (
                      <DropdownMenuItem onClick={() => handleStateAction(monitor.id, "activate")}>
                        <Play className="mr-2 h-4 w-4" />
                        Activate
                      </DropdownMenuItem>
                    )}
                    {monitor.state === "active" && (
                      <DropdownMenuItem onClick={() => handleStateAction(monitor.id, "pause")}>
                        <Pause className="mr-2 h-4 w-4" />
                        Pause
                      </DropdownMenuItem>
                    )}
                    {monitor.state !== "disabled" && (
                      <DropdownMenuItem onClick={() => handleStateAction(monitor.id, "disable")}>
                        <Ban className="mr-2 h-4 w-4" />
                        Disable
                      </DropdownMenuItem>
                    )}
                    {monitor.state === "error" && (
                      <DropdownMenuItem onClick={() => handleStateAction(monitor.id, "acknowledge")}>
                        <AlertTriangle className="mr-2 h-4 w-4" />
                        Acknowledge Error
                      </DropdownMenuItem>
                    )}
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      onClick={() => setDeleteTarget(monitor)}
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
        title="Delete Monitor"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? This action cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteMonitor.isPending}
        onConfirm={handleDelete}
      />
    </>
  );
}
