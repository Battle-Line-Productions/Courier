"use client";

import { useState } from "react";
import Link from "next/link";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { JobTable } from "@/components/jobs/job-table";
import { EmptyState } from "@/components/shared/empty-state";
import { TagFilter } from "@/components/tags/tag-filter";
import { useJobs } from "@/lib/hooks/use-jobs";
import { usePermissions } from "@/lib/hooks/use-permissions";

export default function JobsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [tagFilter, setTagFilter] = useState("");
  const pageSize = 10;

  const filters = {
    search: search || undefined,
    tag: tagFilter || undefined,
  };

  const { data, isLoading } = useJobs(page, pageSize, filters);
  const { can } = usePermissions();

  const jobs = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Jobs</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Create and manage file transfer jobs
          </p>
        </div>
        {can("JobsCreate") && (
          <Button asChild>
            <Link href="/jobs/new">
              <Plus className="mr-2 h-4 w-4" />
              Create Job
            </Link>
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : jobs.length === 0 && !search && !tagFilter ? (
        <EmptyState
          title="No jobs yet"
          description="Create your first file transfer job to get started. Jobs define the steps and configuration for automated file operations."
          actionLabel={can("JobsCreate") ? "Create Job" : undefined}
          actionHref={can("JobsCreate") ? "/jobs/new" : undefined}
        />
      ) : (
        <>
          <div className="flex items-center gap-3">
            <Input
              placeholder="Search jobs..."
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              className="max-w-sm"
            />
            <TagFilter
              value={tagFilter}
              onChange={(v) => {
                setTagFilter(v);
                setPage(1);
              }}
            />
          </div>

          {jobs.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">
              No jobs match your filters.
            </p>
          ) : (
            <JobTable jobs={jobs} />
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
