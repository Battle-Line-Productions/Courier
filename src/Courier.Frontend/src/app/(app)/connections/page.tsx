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
import { ConnectionTable } from "@/components/connections/connection-table";
import { EmptyState } from "@/components/shared/empty-state";
import { TagFilter } from "@/components/tags/tag-filter";
import { useConnections } from "@/lib/hooks/use-connections";
import { usePermissions } from "@/lib/hooks/use-permissions";

export default function ConnectionsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [protocolFilter, setProtocolFilter] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [tagFilter, setTagFilter] = useState<string>("");
  const pageSize = 10;

  const filters = {
    search: search || undefined,
    protocol: protocolFilter || undefined,
    status: statusFilter || undefined,
    tag: tagFilter || undefined,
  };

  const { data, isLoading } = useConnections(page, pageSize, filters);
  const { can } = usePermissions();

  const connections = data?.data ?? [];
  const pagination = data?.pagination;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Connections</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Manage SFTP, FTP, and FTPS server connections
          </p>
        </div>
        {can("ConnectionsCreate") && (
          <Button asChild>
            <Link href="/connections/new">
              <Plus className="mr-2 h-4 w-4" />
              Create Connection
            </Link>
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : connections.length === 0 && !search && !protocolFilter && !statusFilter && !tagFilter ? (
        <EmptyState
          title="No connections yet"
          description="Create your first connection to an SFTP, FTP, or FTPS server. Connections define how Courier communicates with remote file servers."
          actionLabel={can("ConnectionsCreate") ? "Create Connection" : undefined}
          actionHref={can("ConnectionsCreate") ? "/connections/new" : undefined}
        />
      ) : (
        <>
          <div className="flex items-center gap-3">
            <Input
              placeholder="Search connections..."
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              className="max-w-sm"
            />
            <Select
              value={protocolFilter}
              onValueChange={(v) => {
                setProtocolFilter(v === "all" ? "" : v);
                setPage(1);
              }}
            >
              <SelectTrigger className="w-36">
                <SelectValue placeholder="Protocol" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Protocols</SelectItem>
                <SelectItem value="sftp">SFTP</SelectItem>
                <SelectItem value="ftp">FTP</SelectItem>
                <SelectItem value="ftps">FTPS</SelectItem>
              </SelectContent>
            </Select>
            <Select
              value={statusFilter}
              onValueChange={(v) => {
                setStatusFilter(v === "all" ? "" : v);
                setPage(1);
              }}
            >
              <SelectTrigger className="w-36">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="active">Active</SelectItem>
                <SelectItem value="disabled">Disabled</SelectItem>
              </SelectContent>
            </Select>
            <TagFilter
              value={tagFilter}
              onChange={(v) => {
                setTagFilter(v);
                setPage(1);
              }}
            />
          </div>

          {connections.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">
              No connections match your filters.
            </p>
          ) : (
            <ConnectionTable connections={connections} />
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
