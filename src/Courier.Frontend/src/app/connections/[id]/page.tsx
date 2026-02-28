"use client";

import { use } from "react";
import Link from "next/link";
import { useConnection } from "@/lib/hooks/use-connections";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/shared/status-badge";
import { Pencil } from "lucide-react";

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function formatLabel(value: string): string {
  return value
    .replace(/_/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

export default function ConnectionDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useConnection(id);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  const conn = data?.data;
  if (!conn) {
    return <p className="text-muted-foreground">Connection not found.</p>;
  }

  const isSftp = conn.protocol === "sftp";
  const isFtpOrFtps = conn.protocol === "ftp" || conn.protocol === "ftps";

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{conn.name}</h1>
          <div className="mt-3 flex items-center gap-2">
            {conn.group && (
              <Badge variant="secondary" className="text-xs">
                {conn.group}
              </Badge>
            )}
            <Badge variant="secondary" className="font-mono text-xs uppercase">
              {conn.protocol}
            </Badge>
            <StatusBadge state={conn.status} />
            <span className="text-sm text-muted-foreground">
              Created {timeAgo(conn.createdAt)}
            </span>
          </div>
        </div>
        <Button variant="outline" asChild>
          <Link href={`/connections/${id}/edit`}>
            <Pencil className="mr-2 h-4 w-4" />
            Edit
          </Link>
        </Button>
      </div>

      <Separator />

      {/* Connection Info */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Connection Info</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Host</dt>
              <dd className="mt-0.5 font-mono text-sm">{conn.host}:{conn.port}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Protocol</dt>
              <dd className="mt-0.5 text-sm uppercase">{conn.protocol}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Auth Method</dt>
              <dd className="mt-0.5 text-sm">{formatLabel(conn.authMethod)}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Username</dt>
              <dd className="mt-0.5 font-mono text-sm">{conn.username}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Has Password</dt>
              <dd className="mt-0.5 text-sm">{conn.hasPassword ? "Yes" : "No"}</dd>
            </div>
            {conn.sshKeyId && (
              <div>
                <dt className="text-sm font-medium text-muted-foreground">SSH Key</dt>
                <dd className="mt-0.5 font-mono text-sm">{conn.sshKeyId}</dd>
              </div>
            )}
          </dl>
        </CardContent>
      </Card>

      {/* Settings */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Settings</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-4">
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Connect Timeout</dt>
              <dd className="mt-0.5 text-sm">{conn.connectTimeoutSec}s</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Operation Timeout</dt>
              <dd className="mt-0.5 text-sm">{conn.operationTimeoutSec}s</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Keepalive Interval</dt>
              <dd className="mt-0.5 text-sm">{conn.keepaliveIntervalSec}s</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Retries</dt>
              <dd className="mt-0.5 text-sm">{conn.transportRetries}</dd>
            </div>
            {isSftp && (
              <>
                <div>
                  <dt className="text-sm font-medium text-muted-foreground">Host Key Policy</dt>
                  <dd className="mt-0.5 text-sm">{formatLabel(conn.hostKeyPolicy)}</dd>
                </div>
                {conn.sshAlgorithms && (
                  <div className="col-span-2">
                    <dt className="text-sm font-medium text-muted-foreground">SSH Algorithms</dt>
                    <dd className="mt-0.5 font-mono text-xs">{conn.sshAlgorithms}</dd>
                  </div>
                )}
              </>
            )}
            {isFtpOrFtps && (
              <>
                <div>
                  <dt className="text-sm font-medium text-muted-foreground">Passive Mode</dt>
                  <dd className="mt-0.5 text-sm">{conn.passiveMode ? "Yes" : "No"}</dd>
                </div>
                {conn.protocol === "ftps" && (
                  <>
                    {conn.tlsVersionFloor && (
                      <div>
                        <dt className="text-sm font-medium text-muted-foreground">TLS Version Floor</dt>
                        <dd className="mt-0.5 text-sm">{conn.tlsVersionFloor === "tls12" ? "TLS 1.2" : "TLS 1.3"}</dd>
                      </div>
                    )}
                    <div>
                      <dt className="text-sm font-medium text-muted-foreground">TLS Cert Policy</dt>
                      <dd className="mt-0.5 text-sm">{formatLabel(conn.tlsCertPolicy)}</dd>
                    </div>
                  </>
                )}
              </>
            )}
            {conn.fipsOverride && (
              <div>
                <dt className="text-sm font-medium text-muted-foreground">FIPS Override</dt>
                <dd className="mt-0.5 text-sm">Enabled</dd>
              </div>
            )}
          </dl>
        </CardContent>
      </Card>

      {/* Known Hosts */}
      {isSftp && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Known Hosts</CardTitle>
          </CardHeader>
          <CardContent>
            {conn.storedHostFingerprint ? (
              <p className="font-mono text-sm">{conn.storedHostFingerprint}</p>
            ) : (
              <p className="text-sm text-muted-foreground">No known hosts recorded.</p>
            )}
          </CardContent>
        </Card>
      )}

      {/* Notes */}
      {conn.notes && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Notes</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm whitespace-pre-wrap">{conn.notes}</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
