"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { MoreHorizontal, Pencil, Trash2, Download, Archive, RotateCcw } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { StatusBadge } from "@/components/shared/status-badge";
import { useDeleteSshKey, useRetireSshKey, useActivateSshKey } from "@/lib/hooks/use-ssh-key-mutations";
import { api } from "@/lib/api";
import { toast } from "sonner";
import type { SshKeyDto } from "@/lib/types";
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

function formatKeyType(kt: string): string {
  return kt.replace(/_/g, " ").toUpperCase();
}

interface SshKeyTableProps {
  keys: SshKeyDto[];
}

export function SshKeyTable({ keys }: SshKeyTableProps) {
  const router = useRouter();
  const deleteKey = useDeleteSshKey();
  const retireKey = useRetireSshKey();
  const activateKey = useActivateSshKey();
  const [deleteTarget, setDeleteTarget] = useState<SshKeyDto | null>(null);

  function handleDelete() {
    if (!deleteTarget) return;
    deleteKey.mutate(deleteTarget.id, {
      onSuccess: () => { toast.success("SSH key deleted"); setDeleteTarget(null); },
      onError: (error) => { toast.error(error.message); },
    });
  }

  async function handleExport(id: string, name: string) {
    try {
      const blob = await api.exportSshPublicKey(id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${name}.pub`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast.error("Failed to export public key");
    }
  }

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Type</TableHead>
            <TableHead>Fingerprint</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Created</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {keys.map((key) => (
            <TableRow key={key.id} className="group">
              <TableCell>
                <Link
                  href={`/keys/ssh/${key.id}`}
                  className="font-medium text-primary hover:underline underline-offset-4"
                >
                  {key.name}
                </Link>
              </TableCell>
              <TableCell>
                <Badge variant="secondary" className="font-mono text-xs">
                  {formatKeyType(key.keyType)}
                </Badge>
              </TableCell>
              <TableCell className="text-muted-foreground font-mono text-xs">
                {key.fingerprint || "\u2014"}
              </TableCell>
              <TableCell>
                <StatusBadge state={key.status} />
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {timeAgo(key.createdAt)}
              </TableCell>
              <TableCell>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => router.push(`/keys/ssh/${key.id}/edit`)}>
                      <Pencil className="mr-2 h-4 w-4" /> Edit
                    </DropdownMenuItem>
                    {key.hasPublicKey && (
                      <DropdownMenuItem onClick={() => handleExport(key.id, key.name)}>
                        <Download className="mr-2 h-4 w-4" /> Export Public Key
                      </DropdownMenuItem>
                    )}
                    <DropdownMenuSeparator />
                    {key.status === "active" && (
                      <DropdownMenuItem onClick={() => retireKey.mutate(key.id, {
                        onSuccess: () => toast.success("Key retired"),
                        onError: (e) => toast.error(e.message),
                      })}>
                        <Archive className="mr-2 h-4 w-4" /> Retire
                      </DropdownMenuItem>
                    )}
                    {key.status === "retired" && (
                      <DropdownMenuItem onClick={() => activateKey.mutate(key.id, {
                        onSuccess: () => toast.success("Key activated"),
                        onError: (e) => toast.error(e.message),
                      })}>
                        <RotateCcw className="mr-2 h-4 w-4" /> Activate
                      </DropdownMenuItem>
                    )}
                    <DropdownMenuSeparator />
                    <DropdownMenuItem onClick={() => setDeleteTarget(key)} className="text-destructive">
                      <Trash2 className="mr-2 h-4 w-4" /> Delete
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
        title="Delete SSH Key"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? All key material will be permanently purged.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteKey.isPending}
        onConfirm={handleDelete}
      />
    </>
  );
}
