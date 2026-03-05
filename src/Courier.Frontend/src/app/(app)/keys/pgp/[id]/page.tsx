"use client";

import { use } from "react";
import Link from "next/link";
import { usePgpKey } from "@/lib/hooks/use-pgp-keys";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBadge } from "@/components/shared/status-badge";
import { TagPicker } from "@/components/tags/tag-picker";
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

function formatAlgorithm(algo: string): string {
  return algo.replace(/_/g, " ").toUpperCase();
}

export default function PgpKeyDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = usePgpKey(id);

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
    return <p className="text-muted-foreground">PGP key not found.</p>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{key.name}</h1>
          <div className="mt-3 flex items-center gap-2">
            <Badge variant="secondary" className="font-mono text-xs">
              {formatAlgorithm(key.algorithm)}
            </Badge>
            <Badge variant="outline" className="text-xs">
              {key.keyType === "key_pair" ? "Key Pair" : "Public Only"}
            </Badge>
            <StatusBadge state={key.status} />
            <span className="text-sm text-muted-foreground">
              Created {timeAgo(key.createdAt)}
            </span>
          </div>
        </div>
        <Button variant="outline" asChild>
          <Link href={`/keys/pgp/${id}/edit`}>
            <Pencil className="mr-2 h-4 w-4" /> Edit
          </Link>
        </Button>
      </div>

      {/* Tags */}
      <Card>
        <CardHeader><CardTitle className="text-base">Tags</CardTitle></CardHeader>
        <CardContent>
          <TagPicker entityType="pgp_key" entityId={id} currentTags={key.tags} />
        </CardContent>
      </Card>

      <Separator />

      <Card>
        <CardHeader><CardTitle className="text-base">Key Info</CardTitle></CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Fingerprint</dt>
              <dd className="mt-0.5 font-mono text-xs break-all">{key.fingerprint || "\u2014"}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Short Key ID</dt>
              <dd className="mt-0.5 font-mono text-sm">{key.shortKeyId || "\u2014"}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Algorithm</dt>
              <dd className="mt-0.5 text-sm">{formatAlgorithm(key.algorithm)}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Has Public Key</dt>
              <dd className="mt-0.5 text-sm">{key.hasPublicKey ? "Yes" : "No"}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Has Private Key</dt>
              <dd className="mt-0.5 text-sm">{key.hasPrivateKey ? "Yes" : "No"}</dd>
            </div>
            {key.purpose && (
              <div>
                <dt className="text-sm font-medium text-muted-foreground">Purpose</dt>
                <dd className="mt-0.5 text-sm">{key.purpose}</dd>
              </div>
            )}
            {key.expiresAt && (
              <div>
                <dt className="text-sm font-medium text-muted-foreground">Expires</dt>
                <dd className="mt-0.5 text-sm">{new Date(key.expiresAt).toLocaleDateString()}</dd>
              </div>
            )}
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
