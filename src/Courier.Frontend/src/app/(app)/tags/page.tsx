"use client";

import { useMemo, useState } from "react";
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
import { TagTable } from "@/components/tags/tag-table";
import { EmptyState } from "@/components/shared/empty-state";
import { useTags, useAllTags } from "@/lib/hooks/use-tags";
import { usePermissions } from "@/lib/hooks/use-permissions";

export default function TagsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const pageSize = 25;

  const { data, isLoading } = useTags(page, pageSize, {
    search: search || undefined,
    category: categoryFilter || undefined,
  });
  const { data: allTagsData } = useAllTags();
  const { can } = usePermissions();

  const tags = data?.data ?? [];
  const pagination = data?.pagination;

  const categories = useMemo(() => {
    const allTags = allTagsData?.data ?? [];
    const cats = new Set(allTags.map((t) => t.category).filter(Boolean) as string[]);
    return Array.from(cats).sort();
  }, [allTagsData]);

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
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : tags.length === 0 && !search && !categoryFilter ? (
        <EmptyState
          title="No tags yet"
          description="Create your first tag to start organizing your jobs, connections, keys, and monitors."
          actionLabel={can("TagsManage") ? "Create Tag" : undefined}
          actionHref={can("TagsManage") ? "/tags/new" : undefined}
        />
      ) : (
        <>
          <div className="flex items-center gap-3">
            <Input
              placeholder="Search tags..."
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              className="max-w-sm"
            />
            {categories.length > 0 && (
              <Select
                value={categoryFilter}
                onValueChange={(v) => {
                  setCategoryFilter(v === "all" ? "" : v);
                  setPage(1);
                }}
              >
                <SelectTrigger className="w-44">
                  <SelectValue placeholder="Category" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Categories</SelectItem>
                  {categories.map((cat) => (
                    <SelectItem key={cat} value={cat}>
                      {cat}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>

          {tags.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">
              No tags match your filters.
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
