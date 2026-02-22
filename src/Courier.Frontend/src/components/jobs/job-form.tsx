"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { StepBuilder, type StepFormData } from "./step-builder";
import { useCreateJob, useUpdateJob, useReplaceSteps } from "@/lib/hooks/use-job-mutations";
import { api } from "@/lib/api";
import { toast } from "sonner";
import type { JobDto, JobStepDto } from "@/lib/types";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";

const jobSchema = z.object({
  name: z.string().min(1, "Name is required").max(100, "Name must be 100 characters or less"),
  description: z.string().max(500, "Description must be 500 characters or less").optional(),
});

type JobFormValues = z.infer<typeof jobSchema>;

interface JobFormProps {
  job?: JobDto;
  existingSteps?: JobStepDto[];
}

export function JobForm({ job, existingSteps }: JobFormProps) {
  const router = useRouter();
  const isEdit = !!job;

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<JobFormValues>({
    resolver: zodResolver(jobSchema),
    defaultValues: {
      name: job?.name ?? "",
      description: job?.description ?? "",
    },
  });

  const [steps, setSteps] = useState<StepFormData[]>(
    existingSteps?.map((s) => ({
      name: s.name,
      typeKey: s.typeKey,
      configuration: s.configuration,
      timeoutSeconds: s.timeoutSeconds,
    })) ?? []
  );

  const createJob = useCreateJob();
  const updateJob = useUpdateJob(job?.id ?? "");
  const replaceSteps = useReplaceSteps(job?.id ?? "");
  const isSubmitting = createJob.isPending || updateJob.isPending || replaceSteps.isPending;

  async function onSubmit(values: JobFormValues) {
    try {
      if (isEdit) {
        await updateJob.mutateAsync(values);
        await replaceSteps.mutateAsync({
          steps: steps.map((s, i) => ({
            name: s.name,
            typeKey: s.typeKey,
            stepOrder: i + 1,
            configuration: s.configuration,
            timeoutSeconds: s.timeoutSeconds,
          })),
        });
        toast.success("Job updated");
        router.push(`/jobs/${job.id}`);
      } else {
        const result = await createJob.mutateAsync(values);
        const jobId = result.data!.id;

        if (steps.length > 0) {
          await api.replaceSteps(jobId, {
            steps: steps.map((s, i) => ({
              name: s.name,
              typeKey: s.typeKey,
              stepOrder: i + 1,
              configuration: s.configuration,
              timeoutSeconds: s.timeoutSeconds,
            })),
          });
        }

        toast.success("Job created");
        router.push(`/jobs/${jobId}`);
      }
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : "Something went wrong";
      toast.error(message);
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" asChild>
          <Link href={isEdit ? `/jobs/${job.id}` : "/jobs"}>
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </Button>
        <h1 className="text-2xl font-bold tracking-tight">
          {isEdit ? "Edit Job" : "Create Job"}
        </h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Job Details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" placeholder="e.g., Invoice Processor" {...register("name")} />
            {errors.name && (
              <p className="text-sm text-destructive">{errors.name.message}</p>
            )}
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="description">Description</Label>
            <Input
              id="description"
              placeholder="What does this job do?"
              {...register("description")}
            />
            {errors.description && (
              <p className="text-sm text-destructive">{errors.description.message}</p>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="pt-6">
          <StepBuilder steps={steps} onChange={setSteps} />
        </CardContent>
      </Card>

      <div className="flex justify-end gap-3">
        <Button variant="outline" type="button" asChild>
          <Link href={isEdit ? `/jobs/${job.id}` : "/jobs"}>Cancel</Link>
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Saving..." : isEdit ? "Save Changes" : "Create Job"}
        </Button>
      </div>
    </form>
  );
}
