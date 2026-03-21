"use client";

import { useRouter } from "next/navigation";
import { MonitorForm } from "@/components/monitors/monitor-form";
import { usePermissions } from "@/lib/hooks/use-permissions";

export default function NewMonitorPage() {
  const router = useRouter();
  const { can } = usePermissions();

  if (!can("MonitorsCreate")) {
    router.push("/monitors");
    return null;
  }

  return <MonitorForm />;
}
