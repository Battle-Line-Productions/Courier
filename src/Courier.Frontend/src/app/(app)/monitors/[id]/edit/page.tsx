"use client";

import { use } from "react";
import { useMonitor } from "@/lib/hooks/use-monitors";
import { MonitorForm } from "@/components/monitors/monitor-form";
import { Skeleton } from "@/components/ui/skeleton";

export default function EditMonitorPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useMonitor(id);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  const monitor = data?.data;
  if (!monitor) {
    return <p className="text-muted-foreground">Monitor not found.</p>;
  }

  return <MonitorForm monitor={monitor} />;
}
