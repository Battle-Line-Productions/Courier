"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useConnection } from "@/lib/hooks/use-connections";
import { useTestConnection } from "@/lib/hooks/use-connection-mutations";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/shared/status-badge";
import { TagPicker } from "@/components/tags/tag-picker";
import { Pencil, Plug, Loader2, CheckCircle2, XCircle } from "lucide-react";
import type { ConnectionTestDto } from "@/lib/types";
import { usePermissions } from "@/lib/hooks/use-permissions";

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

function parseProperties(props?: string): Record<string, string> {
  if (!props) return {};
  try {
    return JSON.parse(props);
  } catch {
    return {};
  }
}

export default function ConnectionDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useConnection(id);
  const testMutation = useTestConnection();
  const [testResult, setTestResult] = useState<ConnectionTestDto | null>(null);
  const { can } = usePermissions();

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

  const isAzureFunction = conn.protocol === "azure_function";
  const isSftp = conn.protocol === "sftp";
  const isFtpOrFtps = conn.protocol === "ftp" || conn.protocol === "ftps";
  const azureProps = isAzureFunction ? parseProperties(conn.properties) : {};

  const handleTestConnection = () => {
    setTestResult(null);
    testMutation.mutate(id, {
      onSuccess: (response) => {
        if (response.data) {
          setTestResult(response.data);
        }
      },
    });
  };

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
              {conn.protocol === "azure_function" ? "Azure Function" : conn.protocol}
            </Badge>
            <StatusBadge state={conn.status} />
            <span className="text-sm text-muted-foreground">
              Created {timeAgo(conn.createdAt)}
            </span>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {!isAzureFunction && can("ConnectionsTest") && (
            <Button
              variant="outline"
              onClick={handleTestConnection}
              disabled={testMutation.isPending}
            >
              {testMutation.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Plug className="mr-2 h-4 w-4" />
              )}
              Test Connection
            </Button>
          )}
          {can("ConnectionsEdit") && (
            <Button variant="outline" asChild>
              <Link href={`/connections/${id}/edit`}>
                <Pencil className="mr-2 h-4 w-4" />
                Edit
              </Link>
            </Button>
          )}
        </div>
      </div>

      {/* Tags */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Tags</CardTitle>
        </CardHeader>
        <CardContent>
          <TagPicker entityType="connection" entityId={id} currentTags={conn.tags} />
        </CardContent>
      </Card>

      <Separator />

      {/* Test Connection Result */}
      {testResult && (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">Test Result</CardTitle>
              {testResult.connected ? (
                <Badge variant="default" className="bg-green-600">
                  <CheckCircle2 className="mr-1 h-3 w-3" />
                  Connected
                </Badge>
              ) : (
                <Badge variant="destructive">
                  <XCircle className="mr-1 h-3 w-3" />
                  Failed
                </Badge>
              )}
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
              <div>
                <dt className="text-sm font-medium text-muted-foreground">Latency</dt>
                <dd className="mt-0.5 font-mono text-sm">{testResult.latencyMs.toFixed(1)} ms</dd>
              </div>
              {testResult.serverBanner && (
                <div className="col-span-2">
                  <dt className="text-sm font-medium text-muted-foreground">Server Banner</dt>
                  <dd className="mt-0.5 font-mono text-sm">{testResult.serverBanner}</dd>
                </div>
              )}
              {testResult.error && (
                <div className="col-span-full">
                  <dt className="text-sm font-medium text-muted-foreground">Error</dt>
                  <dd className="mt-0.5 text-sm text-destructive">{testResult.error}</dd>
                </div>
              )}
            </dl>

            {/* SSH Algorithm Details */}
            {testResult.supportedAlgorithms && (
              <div className="space-y-2 border-t pt-3">
                <h4 className="text-sm font-medium">SSH Algorithms</h4>
                <dl className="grid grid-cols-1 gap-y-2 sm:grid-cols-2">
                  <div>
                    <dt className="text-xs font-medium text-muted-foreground">Ciphers</dt>
                    <dd className="mt-0.5 font-mono text-xs">{testResult.supportedAlgorithms.cipher.join(", ")}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium text-muted-foreground">Key Exchange</dt>
                    <dd className="mt-0.5 font-mono text-xs">{testResult.supportedAlgorithms.kex.join(", ")}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium text-muted-foreground">MACs</dt>
                    <dd className="mt-0.5 font-mono text-xs">{testResult.supportedAlgorithms.mac.join(", ")}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium text-muted-foreground">Host Key</dt>
                    <dd className="mt-0.5 font-mono text-xs">{testResult.supportedAlgorithms.hostKey.join(", ")}</dd>
                  </div>
                </dl>
              </div>
            )}

            {/* TLS Certificate Details */}
            {testResult.tlsCertificate && (
              <div className="space-y-2 border-t pt-3">
                <h4 className="text-sm font-medium">TLS Certificate</h4>
                <dl className="grid grid-cols-2 gap-x-6 gap-y-2 sm:grid-cols-3">
                  <div>
                    <dt className="text-xs font-medium text-muted-foreground">Subject</dt>
                    <dd className="mt-0.5 font-mono text-xs">{testResult.tlsCertificate.subject}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium text-muted-foreground">Issuer</dt>
                    <dd className="mt-0.5 font-mono text-xs">{testResult.tlsCertificate.issuer}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium text-muted-foreground">Valid From</dt>
                    <dd className="mt-0.5 text-xs">{new Date(testResult.tlsCertificate.validFrom).toLocaleDateString()}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium text-muted-foreground">Valid To</dt>
                    <dd className="mt-0.5 text-xs">{new Date(testResult.tlsCertificate.validTo).toLocaleDateString()}</dd>
                  </div>
                  <div className="col-span-full">
                    <dt className="text-xs font-medium text-muted-foreground">Thumbprint (SHA-256)</dt>
                    <dd className="mt-0.5 font-mono text-xs break-all">{testResult.tlsCertificate.thumbprint}</dd>
                  </div>
                </dl>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Connection Info */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Connection Info</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
            <div>
              <dt className="text-sm font-medium text-muted-foreground">
                {isAzureFunction ? "Function App URL" : "Host"}
              </dt>
              <dd className="mt-0.5 font-mono text-sm">{conn.host}:{conn.port}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Protocol</dt>
              <dd className="mt-0.5 text-sm uppercase">
                {conn.protocol === "azure_function" ? "Azure Function" : conn.protocol}
              </dd>
            </div>
            {!isAzureFunction && (
              <>
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
              </>
            )}
            {isAzureFunction && (
              <>
                <div>
                  <dt className="text-sm font-medium text-muted-foreground">Has Master Key</dt>
                  <dd className="mt-0.5 text-sm">{conn.hasPassword ? "Yes" : "No"}</dd>
                </div>
                <div>
                  <dt className="text-sm font-medium text-muted-foreground">Has Client Secret</dt>
                  <dd className="mt-0.5 text-sm">{conn.hasClientSecret ? "Yes" : "No"}</dd>
                </div>
              </>
            )}
            {conn.sshKeyId && !isAzureFunction && (
              <div>
                <dt className="text-sm font-medium text-muted-foreground">SSH Key</dt>
                <dd className="mt-0.5 font-mono text-sm">{conn.sshKeyId}</dd>
              </div>
            )}
          </dl>
        </CardContent>
      </Card>

      {/* Azure Function Details */}
      {isAzureFunction && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Azure Settings</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
              {azureProps.tenantId && (
                <div>
                  <dt className="text-sm font-medium text-muted-foreground">Tenant ID</dt>
                  <dd className="mt-0.5 font-mono text-sm">{azureProps.tenantId}</dd>
                </div>
              )}
              {azureProps.clientId && (
                <div>
                  <dt className="text-sm font-medium text-muted-foreground">Client ID</dt>
                  <dd className="mt-0.5 font-mono text-sm">{azureProps.clientId}</dd>
                </div>
              )}
              {azureProps.workspaceId && (
                <div>
                  <dt className="text-sm font-medium text-muted-foreground">Workspace ID</dt>
                  <dd className="mt-0.5 font-mono text-sm">{azureProps.workspaceId}</dd>
                </div>
              )}
              {!azureProps.tenantId && !azureProps.clientId && !azureProps.workspaceId && (
                <div className="col-span-full">
                  <p className="text-sm text-muted-foreground">No Azure properties configured.</p>
                </div>
              )}
            </dl>
          </CardContent>
        </Card>
      )}

      {/* Settings (file transfer protocols) */}
      {!isAzureFunction && (
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
      )}

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
