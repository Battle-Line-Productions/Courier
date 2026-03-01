"use client";

import Link from "next/link";
import { Eye } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/shared/status-badge";
import { useActiveMonitors } from "@/lib/hooks/use-dashboard";

function formatRelativeTime(dateStr?: string): string {
  if (!dateStr) return "Never";
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSec = Math.floor(diffMs / 1000);

  if (diffSec < 60) return `${diffSec}s ago`;
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return `${diffHr}h ago`;
  const diffDay = Math.floor(diffHr / 24);
  return `${diffDay}d ago`;
}

export function ActiveMonitorsList() {
  const { data, isLoading } = useActiveMonitors();
  const monitors = data?.data;

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-sm font-medium">Active Monitors</CardTitle>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-16 w-full" />
            ))}
          </div>
        ) : !monitors?.length ? (
          <div className="flex flex-col items-center justify-center py-6 text-center">
            <Eye className="mb-2 h-8 w-8 text-muted-foreground/40" />
            <p className="text-sm text-muted-foreground">No active monitors.</p>
          </div>
        ) : (
          <div className="space-y-2">
            {monitors.map((monitor) => (
              <div
                key={monitor.id}
                className="rounded-lg border p-3"
              >
                <div className="flex items-start justify-between gap-2">
                  <Link
                    href={`/monitors/${monitor.id}`}
                    className="text-sm font-medium text-blue-600 hover:underline"
                  >
                    {monitor.name}
                  </Link>
                  <StatusBadge state={monitor.state} />
                </div>
                <p className="mt-1 truncate text-xs text-muted-foreground" title={monitor.watchTarget}>
                  {monitor.watchTarget}
                </p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  Polled {formatRelativeTime(monitor.lastPolledAt)}
                </p>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
