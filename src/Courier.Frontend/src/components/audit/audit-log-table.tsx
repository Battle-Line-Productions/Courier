"use client";

import { useState } from "react";
import type { AuditLogEntryDto } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ChevronDown, ChevronRight } from "lucide-react";

const entityTypeColors: Record<string, string> = {
  job: "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300",
  job_execution: "bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-300",
  step_execution: "bg-violet-100 text-violet-800 dark:bg-violet-900 dark:text-violet-300",
  connection: "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300",
  pgp_key: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-300",
  ssh_key: "bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-300",
  file_monitor: "bg-cyan-100 text-cyan-800 dark:bg-cyan-900 dark:text-cyan-300",
};

const entityTypeLabels: Record<string, string> = {
  job: "Job",
  job_execution: "Execution",
  step_execution: "Step",
  connection: "Connection",
  pgp_key: "PGP Key",
  ssh_key: "SSH Key",
  file_monitor: "Monitor",
};

function formatTimestamp(iso: string): string {
  return new Date(iso).toLocaleString();
}

function DetailsCell({ details }: { details: string }) {
  const [expanded, setExpanded] = useState(false);

  if (!details || details === "{}") {
    return <span className="text-muted-foreground">—</span>;
  }

  let parsed: Record<string, unknown>;
  try {
    parsed = JSON.parse(details);
  } catch {
    return <span className="text-muted-foreground">{details}</span>;
  }

  if (Object.keys(parsed).length === 0) {
    return <span className="text-muted-foreground">—</span>;
  }

  return (
    <div>
      <Button
        variant="ghost"
        size="sm"
        className="h-6 px-1 text-xs"
        onClick={() => setExpanded(!expanded)}
      >
        {expanded ? <ChevronDown className="h-3 w-3 mr-1" /> : <ChevronRight className="h-3 w-3 mr-1" />}
        {Object.keys(parsed).length} field{Object.keys(parsed).length !== 1 ? "s" : ""}
      </Button>
      {expanded && (
        <pre className="mt-1 text-xs bg-muted p-2 rounded-md overflow-x-auto max-w-md">
          {JSON.stringify(parsed, null, 2)}
        </pre>
      )}
    </div>
  );
}

interface AuditLogTableProps {
  entries: AuditLogEntryDto[];
  isLoading?: boolean;
}

export function AuditLogTable({ entries, isLoading }: AuditLogTableProps) {
  if (isLoading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-12 bg-muted animate-pulse rounded" />
        ))}
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <p className="text-lg font-medium text-muted-foreground">No audit entries found</p>
        <p className="text-sm text-muted-foreground mt-1">
          Audit entries will appear here as operations are performed.
        </p>
      </div>
    );
  }

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-[180px]">Timestamp</TableHead>
            <TableHead className="w-[120px]">Entity Type</TableHead>
            <TableHead className="w-[280px]">Entity ID</TableHead>
            <TableHead className="w-[140px]">Operation</TableHead>
            <TableHead className="w-[120px]">Performed By</TableHead>
            <TableHead>Details</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {entries.map((entry) => (
            <TableRow key={entry.id}>
              <TableCell className="text-sm tabular-nums">
                {formatTimestamp(entry.performedAt)}
              </TableCell>
              <TableCell>
                <Badge
                  variant="secondary"
                  className={entityTypeColors[entry.entityType] || ""}
                >
                  {entityTypeLabels[entry.entityType] || entry.entityType}
                </Badge>
              </TableCell>
              <TableCell className="font-mono text-xs truncate max-w-[280px]">
                {entry.entityId}
              </TableCell>
              <TableCell className="font-medium text-sm">
                {entry.operation}
              </TableCell>
              <TableCell className="text-sm">{entry.performedBy}</TableCell>
              <TableCell>
                <DetailsCell details={entry.details} />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
