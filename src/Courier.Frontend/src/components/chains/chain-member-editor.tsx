"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import { Plus, Trash2, ArrowDown } from "lucide-react";
import { useReplaceChainMembers } from "@/lib/hooks/use-chain-mutations";
import { toast } from "sonner";
import type { JobChainMemberDto, ChainMemberInput, JobDto } from "@/lib/types";

interface MemberRow {
  jobId: string;
  dependsOnIndex: number | null;
  runOnUpstreamFailure: boolean;
}

interface ChainMemberEditorProps {
  chainId: string;
  currentMembers: JobChainMemberDto[];
  availableJobs: JobDto[];
}

export function ChainMemberEditor({
  chainId,
  currentMembers,
  availableJobs,
}: ChainMemberEditorProps) {
  const replaceMembers = useReplaceChainMembers(chainId);

  const [members, setMembers] = useState<MemberRow[]>(() => {
    if (currentMembers.length === 0) return [];

    const sorted = [...currentMembers].sort(
      (a, b) => a.executionOrder - b.executionOrder
    );
    const idToIndex = new Map(sorted.map((m, i) => [m.id, i]));

    return sorted.map((m) => ({
      jobId: m.jobId,
      dependsOnIndex: m.dependsOnMemberId
        ? idToIndex.get(m.dependsOnMemberId) ?? null
        : null,
      runOnUpstreamFailure: m.runOnUpstreamFailure,
    }));
  });

  function addMember() {
    setMembers((prev) => [
      ...prev,
      { jobId: "", dependsOnIndex: prev.length > 0 ? prev.length - 1 : null, runOnUpstreamFailure: false },
    ]);
  }

  function removeMember(index: number) {
    setMembers((prev) => {
      const next = prev.filter((_, i) => i !== index);
      return next.map((m) => ({
        ...m,
        dependsOnIndex:
          m.dependsOnIndex === index
            ? null
            : m.dependsOnIndex !== null && m.dependsOnIndex > index
              ? m.dependsOnIndex - 1
              : m.dependsOnIndex,
      }));
    });
  }

  function updateMember(index: number, updates: Partial<MemberRow>) {
    setMembers((prev) =>
      prev.map((m, i) => (i === index ? { ...m, ...updates } : m))
    );
  }

  function handleSave() {
    const memberInputs: ChainMemberInput[] = members.map((m, i) => ({
      jobId: m.jobId,
      executionOrder: i + 1,
      dependsOnMemberIndex: m.dependsOnIndex ?? undefined,
      runOnUpstreamFailure: m.runOnUpstreamFailure,
    }));

    replaceMembers.mutate(
      { members: memberInputs },
      {
        onSuccess: () => toast.success("Members updated"),
        onError: (error) => toast.error(error.message),
      }
    );
  }

  const hasEmptyJob = members.some((m) => !m.jobId);

  return (
    <div className="space-y-4">
      {members.length === 0 ? (
        <p className="text-sm text-muted-foreground py-4 text-center">
          No members yet. Add jobs to this chain.
        </p>
      ) : (
        <div className="space-y-3">
          {members.map((member, index) => {
            const job = availableJobs.find((j) => j.id === member.jobId);
            return (
              <div key={index}>
                {index > 0 && (
                  <div className="flex justify-center py-1">
                    <ArrowDown className="h-4 w-4 text-muted-foreground" />
                  </div>
                )}
                <Card>
                  <CardContent className="pt-4 pb-4">
                    <div className="flex items-start gap-4">
                      <span className="mt-2 text-sm font-medium text-muted-foreground tabular-nums w-6">
                        {index + 1}.
                      </span>
                      <div className="flex-1 space-y-3">
                        <div className="grid grid-cols-2 gap-3">
                          <div>
                            <Label className="text-xs text-muted-foreground">Job</Label>
                            <Select
                              value={member.jobId}
                              onValueChange={(value) =>
                                updateMember(index, { jobId: value })
                              }
                            >
                              <SelectTrigger className="mt-1">
                                <SelectValue placeholder="Select a job" />
                              </SelectTrigger>
                              <SelectContent>
                                {availableJobs.map((j) => (
                                  <SelectItem key={j.id} value={j.id}>
                                    {j.name}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          </div>
                          <div>
                            <Label className="text-xs text-muted-foreground">
                              Depends On
                            </Label>
                            <Select
                              value={
                                member.dependsOnIndex !== null
                                  ? String(member.dependsOnIndex)
                                  : "none"
                              }
                              onValueChange={(value) =>
                                updateMember(index, {
                                  dependsOnIndex:
                                    value === "none" ? null : Number(value),
                                })
                              }
                            >
                              <SelectTrigger className="mt-1">
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="none">
                                  None (entry point)
                                </SelectItem>
                                {members.map((m, i) => {
                                  if (i >= index) return null;
                                  const depJob = availableJobs.find(
                                    (j) => j.id === m.jobId
                                  );
                                  return (
                                    <SelectItem key={i} value={String(i)}>
                                      {i + 1}. {depJob?.name || "Unselected"}
                                    </SelectItem>
                                  );
                                })}
                              </SelectContent>
                            </Select>
                          </div>
                        </div>
                        {member.dependsOnIndex !== null && (
                          <div className="flex items-center gap-2">
                            <Switch
                              id={`run-on-fail-${index}`}
                              checked={member.runOnUpstreamFailure}
                              onCheckedChange={(checked) =>
                                updateMember(index, {
                                  runOnUpstreamFailure: checked,
                                })
                              }
                            />
                            <Label
                              htmlFor={`run-on-fail-${index}`}
                              className="text-sm"
                            >
                              Run even if upstream fails
                            </Label>
                          </div>
                        )}
                      </div>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="mt-1 text-muted-foreground hover:text-destructive"
                        onClick={() => removeMember(index)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              </div>
            );
          })}
        </div>
      )}

      <div className="flex gap-2">
        <Button variant="outline" size="sm" onClick={addMember}>
          <Plus className="mr-2 h-4 w-4" />
          Add Job
        </Button>
        <Button
          size="sm"
          onClick={handleSave}
          disabled={hasEmptyJob || replaceMembers.isPending}
        >
          {replaceMembers.isPending ? "Saving..." : "Save Members"}
        </Button>
      </div>
    </div>
  );
}
