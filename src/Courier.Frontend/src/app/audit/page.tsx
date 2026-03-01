"use client";

import { useState } from "react";
import { useAuditLog } from "@/lib/hooks/use-audit-log";
import { AuditLogTable } from "@/components/audit/audit-log-table";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { ChevronLeft, ChevronRight } from "lucide-react";

const ENTITY_TYPES = [
  { value: "", label: "All Types" },
  { value: "job", label: "Job" },
  { value: "job_execution", label: "Job Execution" },
  { value: "step_execution", label: "Step Execution" },
  { value: "connection", label: "Connection" },
  { value: "pgp_key", label: "PGP Key" },
  { value: "ssh_key", label: "SSH Key" },
  { value: "file_monitor", label: "File Monitor" },
];

export default function AuditPage() {
  const [page, setPage] = useState(1);
  const [entityType, setEntityType] = useState("");
  const [operation, setOperation] = useState("");
  const [performedBy, setPerformedBy] = useState("");

  const filters = {
    entityType: entityType || undefined,
    operation: operation || undefined,
    performedBy: performedBy || undefined,
  };

  const { data, isLoading } = useAuditLog(page, 25, filters);

  const totalPages = data?.pagination?.totalPages ?? 1;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Audit Log</h1>
        <p className="text-muted-foreground">
          Security and operational event history
        </p>
      </div>

      <div className="flex flex-wrap gap-3">
        <Select
          value={entityType}
          onValueChange={(v) => {
            setEntityType(v);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-[180px]">
            <SelectValue placeholder="All Types" />
          </SelectTrigger>
          <SelectContent>
            {ENTITY_TYPES.map((t) => (
              <SelectItem key={t.value} value={t.value}>
                {t.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Input
          placeholder="Filter by operation..."
          value={operation}
          onChange={(e) => {
            setOperation(e.target.value);
            setPage(1);
          }}
          className="w-[200px]"
        />

        <Input
          placeholder="Filter by performer..."
          value={performedBy}
          onChange={(e) => {
            setPerformedBy(e.target.value);
            setPage(1);
          }}
          className="w-[200px]"
        />
      </div>

      <AuditLogTable
        entries={data?.data ?? []}
        isLoading={isLoading}
      />

      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Page {page} of {totalPages}
            {data?.pagination?.totalCount !== undefined && (
              <> ({data.pagination.totalCount} total entries)</>
            )}
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
            >
              <ChevronLeft className="h-4 w-4 mr-1" />
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
            >
              Next
              <ChevronRight className="h-4 w-4 ml-1" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
