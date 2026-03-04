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
import { MoreHorizontal, Pencil, Trash2 } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { TagBadge } from "./tag-badge";
import { useDeleteTag } from "@/lib/hooks/use-tag-mutations";
import { toast } from "sonner";
import type { TagDto } from "@/lib/types";
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

interface TagTableProps {
  tags: TagDto[];
}

export function TagTable({ tags }: TagTableProps) {
  const router = useRouter();
  const deleteTag = useDeleteTag();
  const [deleteTarget, setDeleteTarget] = useState<TagDto | null>(null);

  function handleDelete() {
    if (!deleteTarget) return;
    deleteTag.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success("Tag deleted");
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
            <TableHead>Category</TableHead>
            <TableHead>Description</TableHead>
            <TableHead>Created</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {tags.map((tag) => (
            <TableRow key={tag.id} className="group">
              <TableCell>
                <Link
                  href={`/tags/${tag.id}`}
                  className="hover:underline underline-offset-4"
                >
                  <TagBadge name={tag.name} color={tag.color} />
                </Link>
              </TableCell>
              <TableCell className="text-muted-foreground">
                {tag.category || "\u2014"}
              </TableCell>
              <TableCell className="text-muted-foreground max-w-[300px] truncate">
                {tag.description || "\u2014"}
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {timeAgo(tag.createdAt)}
              </TableCell>
              <TableCell>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => router.push(`/tags/${tag.id}/edit`)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={() => setDeleteTarget(tag)}
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
        title="Delete Tag"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? This will remove the tag from all entities.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteTag.isPending}
        onConfirm={handleDelete}
      />
    </>
  );
}
