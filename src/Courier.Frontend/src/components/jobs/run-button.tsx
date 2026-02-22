"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Play } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { useTriggerJob } from "@/lib/hooks/use-job-mutations";
import { toast } from "sonner";

interface RunButtonProps {
  jobId: string;
  jobName: string;
  onTriggered?: (executionId: string) => void;
}

export function RunButton({ jobId, jobName, onTriggered }: RunButtonProps) {
  const [open, setOpen] = useState(false);
  const trigger = useTriggerJob(jobId);

  function handleRun() {
    trigger.mutate(undefined, {
      onSuccess: (data) => {
        toast.success("Job queued");
        setOpen(false);
        if (data.data?.id) {
          onTriggered?.(data.data.id);
        }
      },
      onError: (error) => {
        toast.error(error.message);
      },
    });
  }

  return (
    <>
      <Button onClick={() => setOpen(true)}>
        <Play className="mr-2 h-4 w-4" />
        Run Job
      </Button>
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title="Run Job"
        description={`Run "${jobName}" now?`}
        confirmLabel="Run"
        loading={trigger.isPending}
        onConfirm={handleRun}
      />
    </>
  );
}
