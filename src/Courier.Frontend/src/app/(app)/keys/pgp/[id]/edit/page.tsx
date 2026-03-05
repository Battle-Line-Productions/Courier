"use client";

import { use } from "react";
import { usePgpKey } from "@/lib/hooks/use-pgp-keys";
import { PgpKeyEditForm } from "@/components/keys/pgp-key-edit-form";
import { Skeleton } from "@/components/ui/skeleton";

export default function EditPgpKeyPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = usePgpKey(id);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!data?.data) {
    return <p className="text-muted-foreground">PGP key not found.</p>;
  }

  return <PgpKeyEditForm pgpKey={data.data} />;
}
