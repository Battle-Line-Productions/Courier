"use client";

import { useState } from "react";
import Link from "next/link";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { NotificationRuleTable } from "@/components/notifications/notification-rule-table";
import { EmptyState } from "@/components/shared/empty-state";
import { useNotificationRules } from "@/lib/hooks/use-notification-rules";

export default function NotificationsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const pageSize = 25;

  const { data, isLoading } = useNotificationRules(page, pageSize, {
    search: search || undefined,
  });

  const rules = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Notifications</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Configure alerts for job completions, failures, and other events
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" asChild>
            <Link href="/notifications/logs">View Logs</Link>
          </Button>
          <Button asChild>
            <Link href="/notifications/new">
              <Plus className="mr-2 h-4 w-4" />
              Create Rule
            </Link>
          </Button>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-10 w-72" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : rules.length === 0 && !search ? (
        <EmptyState
          title="No notification rules"
          description="Create your first notification rule to get alerted when jobs complete, fail, or time out."
          actionLabel="Create Rule"
          actionHref="/notifications/new"
        />
      ) : (
        <>
          <Input
            placeholder="Search notification rules..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            className="max-w-sm"
          />

          {rules.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">
              No rules match your search.
            </p>
          ) : (
            <NotificationRuleTable rules={rules} />
          )}

          {pagination && pagination.totalPages > 1 && (
            <div className="flex items-center justify-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                Previous
              </Button>
              <span className="text-sm text-muted-foreground tabular-nums">
                Page {pagination.page} of {pagination.totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= pagination.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
