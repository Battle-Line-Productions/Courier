"use client";

import { use } from "react";
import Link from "next/link";
import { useTag, useTagEntities } from "@/lib/hooks/use-tags";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { TagBadge } from "@/components/tags/tag-badge";
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

function entityTypeLabel(type: string): string {
  const labels: Record<string, string> = {
    job: "Jobs",
    connection: "Connections",
    pgp_key: "PGP Keys",
    ssh_key: "SSH Keys",
    monitor: "Monitors",
  };
  return labels[type] || type;
}

function entityLink(entityType: string, entityId: string): string {
  const routes: Record<string, string> = {
    job: `/jobs/${entityId}`,
    connection: `/connections/${entityId}`,
    pgp_key: `/keys/pgp/${entityId}`,
    ssh_key: `/keys/ssh/${entityId}`,
    monitor: `/monitors/${entityId}`,
  };
  return routes[entityType] || "#";
}

export default function TagDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useTag(id);
  const { data: entitiesData, isLoading: entitiesLoading } = useTagEntities(id);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  const tag = data?.data;
  if (!tag) {
    return <p className="text-muted-foreground">Tag not found.</p>;
  }

  const entities = entitiesData?.data ?? [];
  const groupedEntities = entities.reduce<Record<string, string[]>>((acc, entity) => {
    if (!acc[entity.entityType]) acc[entity.entityType] = [];
    acc[entity.entityType].push(entity.entityId);
    return acc;
  }, {});

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold tracking-tight">{tag.name}</h1>
            <TagBadge name={tag.name} color={tag.color} />
          </div>
          <div className="mt-3 flex items-center gap-2">
            {tag.category && (
              <Badge variant="secondary" className="text-xs">
                {tag.category}
              </Badge>
            )}
            <span className="text-sm text-muted-foreground">
              Created {timeAgo(tag.createdAt)}
            </span>
          </div>
          {tag.description && (
            <p className="mt-2 text-sm text-muted-foreground">{tag.description}</p>
          )}
        </div>
        <Button variant="outline" asChild>
          <Link href={`/tags/${id}/edit`}>
            <Pencil className="mr-2 h-4 w-4" />
            Edit
          </Link>
        </Button>
      </div>

      <Separator />

      {/* Tag Info */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Tag Info</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Name</dt>
              <dd className="mt-0.5 text-sm">{tag.name}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Color</dt>
              <dd className="mt-0.5 flex items-center gap-2">
                {tag.color ? (
                  <>
                    <div
                      className="h-4 w-4 rounded border"
                      style={{ backgroundColor: tag.color }}
                    />
                    <span className="font-mono text-sm">{tag.color}</span>
                  </>
                ) : (
                  <span className="text-sm text-muted-foreground">{"\u2014"}</span>
                )}
              </dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Category</dt>
              <dd className="mt-0.5 text-sm">{tag.category || "\u2014"}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Created</dt>
              <dd className="mt-0.5 text-sm">{timeAgo(tag.createdAt)}</dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-muted-foreground">Updated</dt>
              <dd className="mt-0.5 text-sm">{timeAgo(tag.updatedAt)}</dd>
            </div>
          </dl>
        </CardContent>
      </Card>

      {/* Tagged Entities */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            Tagged Entities ({entities.length})
          </CardTitle>
        </CardHeader>
        <CardContent>
          {entitiesLoading ? (
            <Skeleton className="h-24 w-full" />
          ) : entities.length === 0 ? (
            <p className="text-sm text-muted-foreground">No entities are tagged with this tag.</p>
          ) : (
            <div className="space-y-4">
              {Object.entries(groupedEntities).map(([type, ids]) => (
                <div key={type}>
                  <h4 className="text-sm font-medium mb-2">{entityTypeLabel(type)} ({ids.length})</h4>
                  <div className="space-y-1.5">
                    {ids.map((entityId) => (
                      <div key={entityId} className="flex items-center rounded-md border px-3 py-2">
                        <Link
                          href={entityLink(type, entityId)}
                          className="text-sm font-medium text-primary hover:underline underline-offset-4"
                        >
                          {entityId}
                        </Link>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
