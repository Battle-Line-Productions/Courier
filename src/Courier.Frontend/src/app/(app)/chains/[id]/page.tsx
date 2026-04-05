"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useChain, useChainExecutions } from "@/lib/hooks/use-chains";
import { useAllJobs } from "@/lib/hooks/use-jobs";
import { useTriggerChain } from "@/lib/hooks/use-chain-mutations";
import { ChainMemberEditor } from "@/components/chains/chain-member-editor";
import { ChainSchedulePanel } from "@/components/chains/chain-schedule-panel";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { TagPicker } from "@/components/tags/tag-picker";
import { Pencil, Play } from "lucide-react";
import { toast } from "sonner";
import { usePermissions } from "@/lib/hooks/use-permissions";

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function stateColor(state: string): "default" | "secondary" | "destructive" | "outline" {
  switch (state) {
    case "completed":
      return "default";
    case "running":
      return "secondary";
    case "failed":
      return "destructive";
    case "cancelled":
      return "outline";
    default:
      return "secondary";
  }
}

export default function ChainDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data: chainData, isLoading: chainLoading } = useChain(id);
  const { data: jobsData, isLoading: jobsLoading } = useAllJobs();
  const { data: execData, isLoading: execLoading } = useChainExecutions(id);
  const triggerChain = useTriggerChain(id);
  const [showRunDialog, setShowRunDialog] = useState(false);
  const { can } = usePermissions();

  if (chainLoading || jobsLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  const chain = chainData?.data;
  if (!chain) {
    return <p className="text-muted-foreground">Chain not found.</p>;
  }

  const allJobs = jobsData?.data ?? [];
  const executions = execData?.data ?? [];

  function handleRun() {
    triggerChain.mutate(undefined, {
      onSuccess: () => {
        toast.success("Chain triggered");
        setShowRunDialog(false);
      },
      onError: (error) => {
        toast.error(error.message);
      },
    });
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{chain.name}</h1>
          {chain.description && (
            <p className="mt-1 text-muted-foreground">{chain.description}</p>
          )}
          <div className="mt-3 flex items-center gap-2">
            <Badge variant={chain.isEnabled ? "default" : "secondary"}>
              {chain.isEnabled ? "Enabled" : "Disabled"}
            </Badge>
            <Badge variant="secondary" className="text-xs">
              {chain.members.length} member{chain.members.length !== 1 ? "s" : ""}
            </Badge>
            <span className="text-sm text-muted-foreground">
              Created {timeAgo(chain.createdAt)}
            </span>
          </div>
        </div>
        <div className="flex gap-2">
          {can("ChainsEdit") && (
            <Button variant="outline" asChild>
              <Link href={`/chains/${id}/edit`}>
                <Pencil className="mr-2 h-4 w-4" />
                Edit
              </Link>
            </Button>
          )}
          {can("ChainsExecute") && (
            <Button
              onClick={() => setShowRunDialog(true)}
              disabled={!chain.isEnabled || chain.members.length === 0}
            >
              <Play className="mr-2 h-4 w-4" />
              Run
            </Button>
          )}
        </div>
      </div>

      {/* Tags */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Tags</CardTitle>
        </CardHeader>
        <CardContent>
          <TagPicker entityType="job_chain" entityId={id} currentTags={chain.tags} />
        </CardContent>
      </Card>

      <Separator />

      {/* Members */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            Members ({chain.members.length})
          </CardTitle>
        </CardHeader>
        <CardContent>
          <ChainMemberEditor
            chainId={id}
            currentMembers={chain.members}
            availableJobs={allJobs}
          />
        </CardContent>
      </Card>

      {/* Schedules */}
      <ChainSchedulePanel chainId={id} />

      {/* Executions */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Executions</CardTitle>
        </CardHeader>
        <CardContent>
          {execLoading ? (
            <Skeleton className="h-24 w-full" />
          ) : executions.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No executions yet. Run this chain to see execution history.
            </p>
          ) : (
            <div className="space-y-3">
              {executions.map((exec) => (
                <div
                  key={exec.id}
                  className="rounded-md border px-4 py-3 space-y-2"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <Badge variant={stateColor(exec.state)}>
                        {exec.state}
                      </Badge>
                      <span className="text-sm text-muted-foreground">
                        by {exec.triggeredBy}
                      </span>
                    </div>
                    <span className="text-sm text-muted-foreground">
                      {timeAgo(exec.createdAt)}
                    </span>
                  </div>
                  {exec.jobExecutions.length > 0 && (
                    <div className="flex flex-wrap gap-2 pt-1">
                      {exec.jobExecutions.map((je) => (
                        <div
                          key={je.id}
                          className="flex items-center gap-1.5 rounded bg-muted px-2 py-1"
                        >
                          <span className="text-xs font-medium">
                            {je.jobName}
                          </span>
                          <Badge
                            variant={stateColor(je.state)}
                            className="text-[10px] px-1 py-0"
                          >
                            {je.state}
                          </Badge>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmDialog
        open={showRunDialog}
        onOpenChange={setShowRunDialog}
        title="Run Chain"
        description={`Run "${chain.name}" now? This will execute ${chain.members.length} job(s) in order.`}
        confirmLabel="Run"
        loading={triggerChain.isPending}
        onConfirm={handleRun}
      />
    </div>
  );
}
