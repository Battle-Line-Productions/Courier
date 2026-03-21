"use client";

import { useRouter } from "next/navigation";
import { JobForm } from "@/components/jobs/job-form";
import { usePermissions } from "@/lib/hooks/use-permissions";

export default function NewJobPage() {
  const router = useRouter();
  const { can } = usePermissions();

  if (!can("JobsCreate")) {
    router.push("/jobs");
    return null;
  }

  return <JobForm />;
}
