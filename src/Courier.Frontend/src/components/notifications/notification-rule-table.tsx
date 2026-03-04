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
import { MoreHorizontal, Pencil, Trash2, Zap } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { useDeleteNotificationRule, useTestNotificationRule } from "@/lib/hooks/use-notification-mutations";
import { toast } from "sonner";
import type { NotificationRuleDto } from "@/lib/types";
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

interface NotificationRuleTableProps {
  rules: NotificationRuleDto[];
}

export function NotificationRuleTable({ rules }: NotificationRuleTableProps) {
  const router = useRouter();
  const deleteRule = useDeleteNotificationRule();
  const testRule = useTestNotificationRule();
  const [deleteTarget, setDeleteTarget] = useState<NotificationRuleDto | null>(null);

  function handleDelete() {
    if (!deleteTarget) return;
    deleteRule.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success("Notification rule deleted");
        setDeleteTarget(null);
      },
      onError: (error) => toast.error(error.message),
    });
  }

  function handleTest(rule: NotificationRuleDto) {
    testRule.mutate(rule.id, {
      onSuccess: () => toast.success("Test notification sent"),
      onError: (error) => toast.error(`Test failed: ${error.message}`),
    });
  }

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Entity Type</TableHead>
            <TableHead>Channel</TableHead>
            <TableHead>Events</TableHead>
            <TableHead>Enabled</TableHead>
            <TableHead>Created</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {rules.map((rule) => (
            <TableRow key={rule.id} className="group">
              <TableCell>
                <Link
                  href={`/notifications/${rule.id}`}
                  className="font-medium hover:underline underline-offset-4"
                >
                  {rule.name}
                </Link>
              </TableCell>
              <TableCell>
                <Badge variant="outline" className="capitalize">
                  {rule.entityType}
                </Badge>
              </TableCell>
              <TableCell>
                <Badge variant={rule.channel === "webhook" ? "default" : "secondary"}>
                  {rule.channel}
                </Badge>
              </TableCell>
              <TableCell>
                <div className="flex flex-wrap gap-1">
                  {rule.eventTypes.map((et) => (
                    <Badge key={et} variant="outline" className="text-xs">
                      {et.replace(/_/g, " ")}
                    </Badge>
                  ))}
                </div>
              </TableCell>
              <TableCell>
                <Badge variant={rule.isEnabled ? "default" : "secondary"}>
                  {rule.isEnabled ? "Yes" : "No"}
                </Badge>
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {timeAgo(rule.createdAt)}
              </TableCell>
              <TableCell>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => handleTest(rule)}>
                      <Zap className="mr-2 h-4 w-4" />
                      Test
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => router.push(`/notifications/${rule.id}/edit`)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={() => setDeleteTarget(rule)}
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
        title="Delete Notification Rule"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? This action cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteRule.isPending}
        onConfirm={handleDelete}
      />
    </>
  );
}
