"use client";

import { useState } from "react";
import Link from "next/link";
import { ChevronLeft } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { NotificationLogTable } from "@/components/notifications/notification-log-table";
import { useNotificationLogs } from "@/lib/hooks/use-notification-rules";
import { useSearchParams } from "next/navigation";

export default function NotificationLogsPage() {
  const searchParams = useSearchParams();
  const ruleId = searchParams.get("ruleId") ?? undefined;
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const { data, isLoading } = useNotificationLogs(page, pageSize, { ruleId });

  const logs = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/notifications">
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Notification Logs</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            History of all sent notifications
          </p>
        </div>
      </div>

      {isLoading ? (
        <Skeleton className="h-64 w-full" />
      ) : logs.length === 0 ? (
        <p className="text-sm text-muted-foreground py-8 text-center">
          No notification logs found.
        </p>
      ) : (
        <>
          <NotificationLogTable logs={logs} />

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
