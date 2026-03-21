"use client";

import { useState } from "react";
import Link from "next/link";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { TagTable } from "@/components/tags/tag-table";
import { EmptyState } from "@/components/shared/empty-state";
import { useTags } from "@/lib/hooks/use-tags";
import { usePermissions } from "@/lib/hooks/use-permissions";

export default function TagsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const pageSize = 25;

  const { data, isLoading } = useTags(page, pageSize, {
    search: search || undefined,
  });
  const { can } = usePermissions();

  const tags = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Tags</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Organize and categorize resources with tags
          </p>
        </div>
        {can("TagsManage") && (
          <Button asChild>
            <Link href="/tags/new">
              <Plus className="mr-2 h-4 w-4" />
              Create Tag
            </Link>
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-10 w-72" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : tags.length === 0 && !search ? (
        <EmptyState
          title="No tags yet"
          description="Create your first tag to start organizing your jobs, connections, keys, and monitors."
          actionLabel={can("TagsManage") ? "Create Tag" : undefined}
          actionHref={can("TagsManage") ? "/tags/new" : undefined}
        />
      ) : (
        <>
          <Input
            placeholder="Search tags..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            className="max-w-sm"
          />

          {tags.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">
              No tags match your search.
            </p>
          ) : (
            <TagTable tags={tags} />
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
