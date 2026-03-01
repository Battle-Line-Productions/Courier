"use client";

import { useState } from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { useMonitorFileLog } from "@/lib/hooks/use-monitors";
import { Skeleton } from "@/components/ui/skeleton";
import Link from "next/link";

function formatBytes(bytes: number): string {
  if (bytes === 0) return "0 B";
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString();
}

interface FileLogTableProps {
  monitorId: string;
}

export function FileLogTable({ monitorId }: FileLogTableProps) {
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const { data, isLoading } = useMonitorFileLog(monitorId, page, pageSize);

  const logs = data?.data ?? [];
  const pagination = data?.pagination;

  if (isLoading) {
    return <Skeleton className="h-48 w-full" />;
  }

  if (logs.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-4 text-center">
        No file events detected yet.
      </p>
    );
  }

  return (
    <div className="space-y-4">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>File Path</TableHead>
            <TableHead>Size</TableHead>
            <TableHead>Last Modified</TableHead>
            <TableHead>Triggered At</TableHead>
            <TableHead>Execution</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {logs.map((log) => (
            <TableRow key={log.id}>
              <TableCell className="font-mono text-sm max-w-64 truncate">
                {log.filePath}
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {formatBytes(log.fileSize)}
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {formatDate(log.lastModified)}
              </TableCell>
              <TableCell className="text-muted-foreground text-sm">
                {formatDate(log.triggeredAt)}
              </TableCell>
              <TableCell>
                {log.executionId ? (
                  <Link
                    href={`/jobs/executions/${log.executionId}`}
                    className="text-sm text-primary hover:underline underline-offset-4 font-mono"
                  >
                    {log.executionId.slice(0, 8)}...
                  </Link>
                ) : (
                  <span className="text-sm text-muted-foreground">—</span>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

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
    </div>
  );
}
