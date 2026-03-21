"use client";

import { use } from "react";
import Link from "next/link";
import { useSshKey } from "@/lib/hooks/use-ssh-keys";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/shared/status-badge";
import { TagPicker } from "@/components/tags/tag-picker";
import { Pencil } from "lucide-react";
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

function formatKeyType(kt: string): string {
  return kt.replace(/_/g, " ").toUpperCase();
}

export default function SshKeyDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useSshKey(id);
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

  const key = data?.data;
  if (!key) {
    return <p className="text-muted-foreground">SSH key not found.</p>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{key.name}</h1>
          <div className="mt-3 flex items-center gap-2">
            <Badge variant="secondary" className="font-mono text-xs">
              {formatKeyType(key.keyType)}
            </Badge>
            <StatusBadge state={key.status} />
            <span className="text-sm text-muted-foreground">
              Created {timeAgo(key.createdAt)}
            </span>
          </div>
        </div>
        {can("SshKeysManage") && (
          <Button variant="outline" asChild>
            <Link href={`/keys/ssh/${id}/edit`}>
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </Link>
          </Button>
        )}
      </div>

      {/* Tags */}
      <Card>
        <CardHeader><CardTitle className="text-base">Tags</CardTitle></CardHeader>
        <CardContent>
          <TagPicker entityType="ssh_key" entityId={id} currentTags={key.tags} />
        </CardContent>
      </Card>

      <Separator />

      <Card>
        <CardHeader><CardTitle className="text-base">Key Info</CardTitle></CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Fingerprint</dt>
              <dd className="mt-0.5 font-mono text-sm">{key.fingerprint || "\u2014"}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Key Type</dt>
              <dd className="mt-0.5 text-sm">{formatKeyType(key.keyType)}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Has Public Key</dt>
              <dd className="mt-0.5 text-sm">{key.hasPublicKey ? "Yes" : "No"}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Has Private Key</dt>
              <dd className="mt-0.5 text-sm">{key.hasPrivateKey ? "Yes" : "No"}</dd>
            </div>
          </dl>
        </CardContent>
      </Card>

      {key.notes && (
        <Card>
          <CardHeader><CardTitle className="text-base">Notes</CardTitle></CardHeader>
          <CardContent>
            <p className="text-sm whitespace-pre-wrap">{key.notes}</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
