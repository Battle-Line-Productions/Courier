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
import { MoreHorizontal, Pencil, Trash2 } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { StatusBadge } from "@/components/shared/status-badge";
import { useDeleteConnection } from "@/lib/hooks/use-connection-mutations";
import { toast } from "sonner";
import { TagBadge } from "@/components/tags/tag-badge";
import type { ConnectionDto } from "@/lib/types";
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

interface ConnectionTableProps {
  connections: ConnectionDto[];
}

export function ConnectionTable({ connections }: ConnectionTableProps) {
  const router = useRouter();
  const deleteConnection = useDeleteConnection();
  const [deleteTarget, setDeleteTarget] = useState<ConnectionDto | null>(null);

  function handleDelete() {
    if (!deleteTarget) return;
    deleteConnection.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success("Connection deleted");
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
            <TableHead>Group</TableHead>
            <TableHead>Protocol</TableHead>
            <TableHead>Host</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Tags</TableHead>
            <TableHead>Created</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {connections.map((conn) => (
            <TableRow key={conn.id} className="group">
              <TableCell>
                <Link
                  href={`/connections/${conn.id}`}
                  className="font-medium text-primary hover:underline underline-offset-4"
                >
                  {conn.name}
                </Link>
              </TableCell>
              <TableCell className="text-muted-foreground">
                {conn.group || "\u2014"}
              </TableCell>
              <TableCell>
                <Badge variant="secondary" className="font-mono text-xs uppercase">
                  {conn.protocol}
                </Badge>
              </TableCell>
              <TableCell className="text-muted-foreground font-mono text-sm">
                {conn.host}:{conn.port}
              </TableCell>
              <TableCell>
                <StatusBadge state={conn.status} />
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-1">
                  {conn.tags?.map((tag) => (
                    <TagBadge key={tag.name} name={tag.name} color={tag.color} />
                  ))}
                </div>
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {timeAgo(conn.createdAt)}
              </TableCell>
              <TableCell>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => router.push(`/connections/${conn.id}/edit`)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={() => setDeleteTarget(conn)}
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
        title="Delete Connection"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? This action cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteConnection.isPending}
        onConfirm={handleDelete}
      />
    </>
  );
}
