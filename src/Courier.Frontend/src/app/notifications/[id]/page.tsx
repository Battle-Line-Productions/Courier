"use client";

import { use, useState } from "react";
import Link from "next/link";
import { ChevronLeft, Pencil, Trash2, Zap } from "lucide-react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { NotificationLogTable } from "@/components/notifications/notification-log-table";
import { useNotificationRule, useNotificationLogs } from "@/lib/hooks/use-notification-rules";
import { useDeleteNotificationRule, useTestNotificationRule } from "@/lib/hooks/use-notification-mutations";
import { toast } from "sonner";

export default function NotificationRuleDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const { data, isLoading } = useNotificationRule(id);
  const { data: logsData } = useNotificationLogs(1, 10, { ruleId: id });
  const deleteRule = useDeleteNotificationRule();
  const testRule = useTestNotificationRule();
  const [showDelete, setShowDelete] = useState(false);

  const rule = data?.data;
  const logs = logsData?.data ?? [];

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!rule) {
    return (
      <div className="text-center py-12">
        <p className="text-muted-foreground">Notification rule not found.</p>
        <Button asChild className="mt-4">
          <Link href="/notifications">Back to Notifications</Link>
        </Button>
      </div>
    );
  }

  function handleDelete() {
    deleteRule.mutate(id, {
      onSuccess: () => {
        toast.success("Rule deleted");
        router.push("/notifications");
      },
      onError: (error) => toast.error(error.message),
    });
  }

  function handleTest() {
    testRule.mutate(id, {
      onSuccess: () => toast.success("Test notification sent"),
      onError: (error) => toast.error(`Test failed: ${error.message}`),
    });
  }

  const config = rule.channelConfig as Record<string, unknown>;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" asChild>
            <Link href="/notifications">
              <ChevronLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-bold tracking-tight">{rule.name}</h1>
            {rule.description && (
              <p className="text-sm text-muted-foreground mt-0.5">{rule.description}</p>
            )}
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={handleTest} disabled={testRule.isPending}>
            <Zap className="mr-2 h-4 w-4" />
            Test
          </Button>
          <Button variant="outline" size="sm" asChild>
            <Link href={`/notifications/${id}/edit`}>
              <Pencil className="mr-2 h-4 w-4" />
              Edit
            </Link>
          </Button>
          <Button variant="destructive" size="sm" onClick={() => setShowDelete(true)}>
            <Trash2 className="mr-2 h-4 w-4" />
            Delete
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-6">
        <div className="rounded-lg border p-4 space-y-3">
          <h2 className="font-semibold text-sm">Configuration</h2>
          <div className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Entity Type</span>
              <Badge variant="outline" className="capitalize">{rule.entityType}</Badge>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Entity ID</span>
              <span>{rule.entityId || "All"}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Channel</span>
              <Badge variant={rule.channel === "webhook" ? "default" : "secondary"}>
                {rule.channel}
              </Badge>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Enabled</span>
              <Badge variant={rule.isEnabled ? "default" : "secondary"}>
                {rule.isEnabled ? "Yes" : "No"}
              </Badge>
            </div>
          </div>
        </div>

        <div className="rounded-lg border p-4 space-y-3">
          <h2 className="font-semibold text-sm">Events & Channel Config</h2>
          <div className="space-y-2 text-sm">
            <div>
              <span className="text-muted-foreground">Events:</span>
              <div className="flex flex-wrap gap-1 mt-1">
                {rule.eventTypes.map((et) => (
                  <Badge key={et} variant="outline" className="text-xs">
                    {et.replace(/_/g, " ")}
                  </Badge>
                ))}
              </div>
            </div>
            {rule.channel === "webhook" && config.url && (
              <div className="flex justify-between">
                <span className="text-muted-foreground">URL</span>
                <span className="text-xs truncate max-w-[200px]">{String(config.url)}</span>
              </div>
            )}
            {rule.channel === "email" && config.recipients && (
              <div>
                <span className="text-muted-foreground">Recipients:</span>
                <p className="text-xs mt-1">{(config.recipients as string[]).join(", ")}</p>
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Recent Notifications</h2>
          <Button variant="ghost" size="sm" asChild>
            <Link href={`/notifications/logs?ruleId=${id}`}>View All</Link>
          </Button>
        </div>
        {logs.length === 0 ? (
          <p className="text-sm text-muted-foreground py-4 text-center">No notifications sent yet.</p>
        ) : (
          <NotificationLogTable logs={logs} />
        )}
      </div>

      <ConfirmDialog
        open={showDelete}
        onOpenChange={setShowDelete}
        title="Delete Notification Rule"
        description={`Are you sure you want to delete "${rule.name}"? This action cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteRule.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
