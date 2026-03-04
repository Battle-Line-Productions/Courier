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
import { MoreHorizontal, Pencil, Trash2, Download, Archive, ShieldOff, RotateCcw } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { StatusBadge } from "@/components/shared/status-badge";
import { useDeletePgpKey, useRetirePgpKey, useRevokePgpKey, useActivatePgpKey } from "@/lib/hooks/use-pgp-key-mutations";
import { api } from "@/lib/api";
import { toast } from "sonner";
import { TagBadge } from "@/components/tags/tag-badge";
import type { PgpKeyDto } from "@/lib/types";
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

function formatAlgorithm(algo: string): string {
  return algo.replace(/_/g, " ").toUpperCase();
}

interface PgpKeyTableProps {
  keys: PgpKeyDto[];
}

export function PgpKeyTable({ keys }: PgpKeyTableProps) {
  const router = useRouter();
  const deleteKey = useDeletePgpKey();
  const retireKey = useRetirePgpKey();
  const revokeKey = useRevokePgpKey();
  const activateKey = useActivatePgpKey();
  const [deleteTarget, setDeleteTarget] = useState<PgpKeyDto | null>(null);

  function handleDelete() {
    if (!deleteTarget) return;
    deleteKey.mutate(deleteTarget.id, {
      onSuccess: () => { toast.success("PGP key deleted"); setDeleteTarget(null); },
      onError: (error) => { toast.error(error.message); },
    });
  }

  async function handleExport(id: string) {
    try {
      const blob = await api.exportPgpPublicKey(id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = "public.asc";
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
            <TableHead>Algorithm</TableHead>
            <TableHead>Type</TableHead>
            <TableHead>Fingerprint</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Tags</TableHead>
            <TableHead>Expires</TableHead>
            <TableHead>Created</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {keys.map((key) => (
            <TableRow key={key.id} className="group">
              <TableCell>
                <Link
                  href={`/keys/pgp/${key.id}`}
                  className="font-medium text-primary hover:underline underline-offset-4"
                >
                  {key.name}
                </Link>
              </TableCell>
              <TableCell>
                <Badge variant="secondary" className="font-mono text-xs">
                  {formatAlgorithm(key.algorithm)}
                </Badge>
              </TableCell>
              <TableCell>
                <Badge variant="outline" className="text-xs">
                  {key.keyType === "key_pair" ? "Key Pair" : "Public Only"}
                </Badge>
              </TableCell>
              <TableCell className="text-muted-foreground font-mono text-xs">
                {key.fingerprint ? `${key.fingerprint.slice(0, 8)}...${key.fingerprint.slice(-8)}` : "\u2014"}
              </TableCell>
              <TableCell>
                <StatusBadge state={key.status} />
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-1">
                  {key.tags?.map((tag) => (
                    <TagBadge key={tag.name} name={tag.name} color={tag.color} />
                  ))}
                </div>
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {key.expiresAt ? new Date(key.expiresAt).toLocaleDateString() : "\u2014"}
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
                    <DropdownMenuItem onClick={() => router.push(`/keys/pgp/${key.id}/edit`)}>
                      <Pencil className="mr-2 h-4 w-4" /> Edit
                    </DropdownMenuItem>
                    {key.hasPublicKey && (
                      <DropdownMenuItem onClick={() => handleExport(key.id)}>
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
                    {key.status !== "revoked" && key.status !== "deleted" && (
                      <DropdownMenuItem onClick={() => revokeKey.mutate(key.id, {
                        onSuccess: () => toast.success("Key revoked"),
                        onError: (e) => toast.error(e.message),
                      })} className="text-destructive">
                        <ShieldOff className="mr-2 h-4 w-4" /> Revoke
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
        title="Delete PGP Key"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? All key material will be permanently purged.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteKey.isPending}
        onConfirm={handleDelete}
      />
    </>
  );
}
