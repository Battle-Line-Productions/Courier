"use client";

import { use } from "react";
import { useConnection } from "@/lib/hooks/use-connections";
import { ConnectionForm } from "@/components/connections/connection-form";
import { Skeleton } from "@/components/ui/skeleton";

export default function EditConnectionPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useConnection(id);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!data?.data) {
    return <p className="text-muted-foreground">Connection not found.</p>;
  }

  return <ConnectionForm connection={data.data} />;
}
