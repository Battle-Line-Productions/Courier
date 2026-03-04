"use client";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import type { NotificationLogDto } from "@/lib/types";

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString();
}

interface NotificationLogTableProps {
  logs: NotificationLogDto[];
}

export function NotificationLogTable({ logs }: NotificationLogTableProps) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Event</TableHead>
          <TableHead>Entity</TableHead>
          <TableHead>Channel</TableHead>
          <TableHead>Recipient</TableHead>
          <TableHead>Status</TableHead>
          <TableHead>Sent At</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {logs.map((log) => (
          <TableRow key={log.id}>
            <TableCell>
              <Badge variant="outline" className="text-xs">
                {log.eventType.replace(/_/g, " ")}
              </Badge>
            </TableCell>
            <TableCell className="text-sm">
              <span className="capitalize">{log.entityType}</span>
              <span className="text-muted-foreground ml-1 text-xs">{log.entityId.slice(0, 8)}</span>
            </TableCell>
            <TableCell>
              <Badge variant={log.channel === "webhook" ? "default" : "secondary"}>
                {log.channel}
              </Badge>
            </TableCell>
            <TableCell className="text-sm text-muted-foreground max-w-[200px] truncate">
              {log.recipient}
            </TableCell>
            <TableCell>
              <Badge variant={log.success ? "default" : "destructive"}>
                {log.success ? "Sent" : "Failed"}
              </Badge>
              {log.errorMessage && (
                <p className="text-xs text-destructive mt-1 max-w-[200px] truncate">
                  {log.errorMessage}
                </p>
              )}
            </TableCell>
            <TableCell className="text-sm text-muted-foreground">
              {formatDate(log.sentAt)}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
