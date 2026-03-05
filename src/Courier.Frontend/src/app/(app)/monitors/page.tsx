"use client";

import { useState } from "react";
import Link from "next/link";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { MonitorTable } from "@/components/monitors/monitor-table";
import { EmptyState } from "@/components/shared/empty-state";
import { useMonitors } from "@/lib/hooks/use-monitors";

export default function MonitorsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [stateFilter, setStateFilter] = useState<string>("");
  const pageSize = 10;

  const filters = {
    search: search || undefined,
    state: stateFilter || undefined,
  };

  const { data, isLoading } = useMonitors(page, pageSize, filters);

  const monitors = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Monitors</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Watch directories for file events and trigger jobs automatically
          </p>
        </div>
        <Button asChild>
          <Link href="/monitors/new">
            <Plus className="mr-2 h-4 w-4" />
            Create Monitor
          </Link>
        </Button>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : monitors.length === 0 && !search && !stateFilter ? (
        <EmptyState
          title="No monitors yet"
          description="Create your first monitor to watch a directory for file events. When files are created or modified, Courier will automatically trigger bound jobs."
          actionLabel="Create Monitor"
          actionHref="/monitors/new"
        />
      ) : (
        <>
          <div className="flex items-center gap-3">
            <Input
              placeholder="Search monitors..."
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              className="max-w-sm"
            />
            <Select
              value={stateFilter}
              onValueChange={(v) => {
                setStateFilter(v === "all" ? "" : v);
                setPage(1);
              }}
            >
              <SelectTrigger className="w-36">
                <SelectValue placeholder="State" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All States</SelectItem>
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="paused">Paused</SelectItem>
                <SelectItem value="disabled">Disabled</SelectItem>
                <SelectItem value="error">Error</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {monitors.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">
              No monitors match your filters.
            </p>
          ) : (
            <MonitorTable monitors={monitors} />
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
