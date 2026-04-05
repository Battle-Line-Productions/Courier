"use client";

import { useState } from "react";
import Link from "next/link";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { ChainTable } from "@/components/chains/chain-table";
import { EmptyState } from "@/components/shared/empty-state";
import { TagFilter } from "@/components/tags/tag-filter";
import { useChains } from "@/lib/hooks/use-chains";
import { usePermissions } from "@/lib/hooks/use-permissions";

export default function ChainsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [tagFilter, setTagFilter] = useState("");
  const pageSize = 10;

  const filters = {
    search: search || undefined,
    tag: tagFilter || undefined,
  };

  const { data, isLoading } = useChains(page, pageSize, filters);
  const { can } = usePermissions();

  const chains = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Chains</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Orchestrate multiple jobs as a sequence
          </p>
        </div>
        {can("ChainsCreate") && (
          <Button asChild>
            <Link href="/chains/new">
              <Plus className="mr-2 h-4 w-4" />
              Create Chain
            </Link>
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : chains.length === 0 && !search && !tagFilter ? (
        <EmptyState
          title="No chains yet"
          description="Create your first chain to orchestrate multiple jobs in sequence."
          actionLabel={can("ChainsCreate") ? "Create Chain" : undefined}
          actionHref={can("ChainsCreate") ? "/chains/new" : undefined}
        />
      ) : (
        <>
          <div className="flex items-center gap-3">
            <Input
              placeholder="Search chains..."
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

          {chains.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">
              No chains match your filters.
            </p>
          ) : (
            <ChainTable chains={chains} />
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
