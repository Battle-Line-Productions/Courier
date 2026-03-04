"use client";

import { use } from "react";
import { useTag } from "@/lib/hooks/use-tags";
import { TagForm } from "@/components/tags/tag-form";
import { Skeleton } from "@/components/ui/skeleton";

export default function EditTagPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useTag(id);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  const tag = data?.data;
  if (!tag) {
    return <p className="text-muted-foreground">Tag not found.</p>;
  }

  return <TagForm tag={tag} />;
}
