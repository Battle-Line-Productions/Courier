"use client";

import { useRouter } from "next/navigation";
import { ConnectionForm } from "@/components/connections/connection-form";
import { usePermissions } from "@/lib/hooks/use-permissions";

export default function NewConnectionPage() {
  const router = useRouter();
  const { can } = usePermissions();

  if (!can("ConnectionsCreate")) {
    router.push("/connections");
    return null;
  }

  return <ConnectionForm />;
}
