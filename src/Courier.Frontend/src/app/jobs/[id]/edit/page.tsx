"use client";

import { use } from "react";
import { useJob } from "@/lib/hooks/use-jobs";
import { useJobSteps } from "@/lib/hooks/use-job-steps";
import { JobForm } from "@/components/jobs/job-form";
import { Skeleton } from "@/components/ui/skeleton";

export default function EditJobPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data: jobData, isLoading: jobLoading } = useJob(id);
  const { data: stepsData, isLoading: stepsLoading } = useJobSteps(id);

  if (jobLoading || stepsLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!jobData?.data) {
    return <p className="text-muted-foreground">Job not found.</p>;
  }

  return <JobForm job={jobData.data} existingSteps={stepsData?.data ?? []} />;
}
