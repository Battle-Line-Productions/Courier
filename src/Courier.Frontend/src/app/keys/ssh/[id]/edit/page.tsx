"use client";

import { use } from "react";
import { useSshKey } from "@/lib/hooks/use-ssh-keys";
import { SshKeyEditForm } from "@/components/keys/ssh-key-edit-form";
import { Skeleton } from "@/components/ui/skeleton";

export default function EditSshKeyPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useSshKey(id);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!data?.data) {
    return <p className="text-muted-foreground">SSH key not found.</p>;
  }

  return <SshKeyEditForm sshKey={data.data} />;
}
