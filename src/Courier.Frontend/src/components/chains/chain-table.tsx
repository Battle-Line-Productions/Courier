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
import { useDeleteChain, useTriggerChain } from "@/lib/hooks/use-chain-mutations";
import { toast } from "sonner";
import type { JobChainDto } from "@/lib/types";

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

interface ChainTableProps {
  chains: JobChainDto[];
}

export function ChainTable({ chains }: ChainTableProps) {
  const router = useRouter();
  const deleteChain = useDeleteChain();
  const [deleteTarget, setDeleteTarget] = useState<JobChainDto | null>(null);
  const [runTarget, setRunTarget] = useState<JobChainDto | null>(null);

  function handleDelete() {
    if (!deleteTarget) return;
    deleteChain.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success("Chain deleted");
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
            <TableHead>Members</TableHead>
            <TableHead>Enabled</TableHead>
            <TableHead>Created</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {chains.map((chain) => (
            <TableRow
              key={chain.id}
              className="group cursor-pointer"
              onClick={() => router.push(`/chains/${chain.id}`)}
            >
              <TableCell>
                <span className="font-medium text-primary">
                  {chain.name}
                </span>
              </TableCell>
              <TableCell className="text-muted-foreground max-w-[200px] truncate">
                {chain.description || "\u2014"}
              </TableCell>
              <TableCell>
                <Badge variant="secondary" className="text-xs">
                  {chain.members.length} job{chain.members.length !== 1 ? "s" : ""}
                </Badge>
              </TableCell>
              <TableCell>
                <Badge variant={chain.isEnabled ? "default" : "secondary"}>
                  {chain.isEnabled ? "Yes" : "No"}
                </Badge>
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {timeAgo(chain.createdAt)}
              </TableCell>
              <TableCell onClick={(e) => e.stopPropagation()}>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => router.push(`/chains/${chain.id}`)}>
                      <Eye className="mr-2 h-4 w-4" />
                      View
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => router.push(`/chains/${chain.id}/edit`)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={() => setRunTarget(chain)}
                      disabled={!chain.isEnabled || chain.members.length === 0}
                    >
                      <Play className="mr-2 h-4 w-4" />
                      Run
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={() => setDeleteTarget(chain)}
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
        title="Delete Chain"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? This action cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteChain.isPending}
        onConfirm={handleDelete}
      />

      {runTarget && (
        <RunChainDialog chain={runTarget} onClose={() => setRunTarget(null)} />
      )}
    </>
  );
}

function RunChainDialog({ chain, onClose }: { chain: JobChainDto; onClose: () => void }) {
  const trigger = useTriggerChain(chain.id);
  const router = useRouter();

  function handleRun() {
    trigger.mutate(undefined, {
      onSuccess: () => {
        toast.success("Chain triggered");
        onClose();
        router.push(`/chains/${chain.id}`);
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
      title="Run Chain"
      description={`Run "${chain.name}" now? This will execute ${chain.members.length} job(s) in order.`}
      confirmLabel="Run"
      loading={trigger.isPending}
      onConfirm={handleRun}
    />
  );
}
