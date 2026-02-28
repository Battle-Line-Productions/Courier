"use client";

import { useState } from "react";
import { useJobSchedules, useCreateSchedule, useUpdateSchedule, useDeleteSchedule } from "@/lib/hooks/use-job-schedules";
import type { JobScheduleDto, CreateJobScheduleRequest, UpdateJobScheduleRequest } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { toast } from "sonner";
import { Plus, Clock, Calendar, Trash2, Pencil } from "lucide-react";

interface SchedulePanelProps {
  jobId: string;
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return "-";
  return new Date(dateStr).toLocaleString();
}

export function SchedulePanel({ jobId }: SchedulePanelProps) {
  const { data, isLoading } = useJobSchedules(jobId);
  const createSchedule = useCreateSchedule(jobId);
  const updateSchedule = useUpdateSchedule(jobId);
  const deleteSchedule = useDeleteSchedule(jobId);

  const [showAdd, setShowAdd] = useState(false);
  const [editingSchedule, setEditingSchedule] = useState<JobScheduleDto | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  // Add form state
  const [scheduleType, setScheduleType] = useState<string>("cron");
  const [cronExpression, setCronExpression] = useState("");
  const [runAt, setRunAt] = useState("");
  const [isEnabled, setIsEnabled] = useState(true);

  // Edit form state
  const [editCron, setEditCron] = useState("");
  const [editRunAt, setEditRunAt] = useState("");
  const [editEnabled, setEditEnabled] = useState(true);

  const schedules = data?.data ?? [];

  function resetAddForm() {
    setScheduleType("cron");
    setCronExpression("");
    setRunAt("");
    setIsEnabled(true);
  }

  function openEdit(s: JobScheduleDto) {
    setEditingSchedule(s);
    setEditCron(s.cronExpression ?? "");
    setEditRunAt(s.runAt ? new Date(s.runAt).toISOString().slice(0, 16) : "");
    setEditEnabled(s.isEnabled);
  }

  function handleCreate() {
    const req: CreateJobScheduleRequest = {
      scheduleType,
      isEnabled,
      ...(scheduleType === "cron" ? { cronExpression } : { runAt: new Date(runAt).toISOString() }),
    };

    createSchedule.mutate(req, {
      onSuccess: () => {
        toast.success("Schedule created");
        setShowAdd(false);
        resetAddForm();
      },
      onError: (err) => toast.error(err.message),
    });
  }

  function handleUpdate() {
    if (!editingSchedule) return;

    const req: UpdateJobScheduleRequest = {
      isEnabled: editEnabled,
      ...(editingSchedule.scheduleType === "cron" && editCron ? { cronExpression: editCron } : {}),
      ...(editingSchedule.scheduleType === "one_shot" && editRunAt
        ? { runAt: new Date(editRunAt).toISOString() }
        : {}),
    };

    updateSchedule.mutate(
      { scheduleId: editingSchedule.id, data: req },
      {
        onSuccess: () => {
          toast.success("Schedule updated");
          setEditingSchedule(null);
        },
        onError: (err) => toast.error(err.message),
      }
    );
  }

  function handleDelete() {
    if (!deletingId) return;
    deleteSchedule.mutate(deletingId, {
      onSuccess: () => {
        toast.success("Schedule deleted");
        setDeletingId(null);
      },
      onError: (err) => toast.error(err.message),
    });
  }

  function handleToggle(s: JobScheduleDto) {
    updateSchedule.mutate(
      { scheduleId: s.id, data: { isEnabled: !s.isEnabled } },
      {
        onSuccess: () => toast.success(s.isEnabled ? "Schedule disabled" : "Schedule enabled"),
        onError: (err) => toast.error(err.message),
      }
    );
  }

  return (
    <>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0">
          <CardTitle className="text-base">Schedules ({isLoading ? "..." : schedules.length})</CardTitle>
          <Button size="sm" variant="outline" onClick={() => setShowAdd(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Add Schedule
          </Button>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <p className="text-sm text-muted-foreground">Loading...</p>
          ) : schedules.length === 0 ? (
            <p className="text-sm text-muted-foreground">No schedules configured.</p>
          ) : (
            <div className="space-y-2">
              {schedules.map((s) => (
                <div
                  key={s.id}
                  className="flex items-center gap-3 rounded-md border px-4 py-3"
                >
                  {s.scheduleType === "cron" ? (
                    <Clock className="h-4 w-4 text-muted-foreground shrink-0" />
                  ) : (
                    <Calendar className="h-4 w-4 text-muted-foreground shrink-0" />
                  )}
                  <Badge variant="secondary" className="text-xs font-mono">
                    {s.scheduleType}
                  </Badge>
                  <span className="font-mono text-sm">
                    {s.scheduleType === "cron" ? s.cronExpression : formatDate(s.runAt)}
                  </span>
                  <span className="ml-auto text-xs text-muted-foreground">
                    Next: {formatDate(s.nextFireAt)}
                  </span>
                  <Switch
                    checked={s.isEnabled}
                    onCheckedChange={() => handleToggle(s)}
                    aria-label="Toggle schedule"
                  />
                  <Button size="icon" variant="ghost" onClick={() => openEdit(s)}>
                    <Pencil className="h-4 w-4" />
                  </Button>
                  <Button size="icon" variant="ghost" onClick={() => setDeletingId(s.id)}>
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Add Schedule Dialog */}
      <Dialog open={showAdd} onOpenChange={(open) => { setShowAdd(open); if (!open) resetAddForm(); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add Schedule</DialogTitle>
            <DialogDescription>Create a new schedule for this job.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Schedule Type</Label>
              <Select value={scheduleType} onValueChange={setScheduleType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="cron">Cron</SelectItem>
                  <SelectItem value="one_shot">One-Shot</SelectItem>
                </SelectContent>
              </Select>
            </div>
            {scheduleType === "cron" ? (
              <div className="space-y-2">
                <Label>Cron Expression</Label>
                <Input
                  placeholder="0 0 3 * * ? (Quartz 7-part format)"
                  value={cronExpression}
                  onChange={(e) => setCronExpression(e.target.value)}
                />
                <p className="text-xs text-muted-foreground">
                  Format: sec min hour day month weekday [year]. Example: 0 0 3 * * ? = daily at 3 AM
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                <Label>Run At</Label>
                <Input
                  type="datetime-local"
                  value={runAt}
                  onChange={(e) => setRunAt(e.target.value)}
                />
              </div>
            )}
            <div className="flex items-center gap-2">
              <Switch checked={isEnabled} onCheckedChange={setIsEnabled} id="add-enabled" />
              <Label htmlFor="add-enabled">Enabled</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowAdd(false)}>
              Cancel
            </Button>
            <Button onClick={handleCreate} disabled={createSchedule.isPending}>
              {createSchedule.isPending ? "Creating..." : "Create"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit Schedule Dialog */}
      <Dialog open={!!editingSchedule} onOpenChange={(open) => { if (!open) setEditingSchedule(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit Schedule</DialogTitle>
            <DialogDescription>Update schedule configuration.</DialogDescription>
          </DialogHeader>
          {editingSchedule && (
            <div className="space-y-4">
              {editingSchedule.scheduleType === "cron" ? (
                <div className="space-y-2">
                  <Label>Cron Expression</Label>
                  <Input
                    placeholder="0 0 3 * * ?"
                    value={editCron}
                    onChange={(e) => setEditCron(e.target.value)}
                  />
                  <p className="text-xs text-muted-foreground">
                    Format: sec min hour day month weekday [year]
                  </p>
                </div>
              ) : (
                <div className="space-y-2">
                  <Label>Run At</Label>
                  <Input
                    type="datetime-local"
                    value={editRunAt}
                    onChange={(e) => setEditRunAt(e.target.value)}
                  />
                </div>
              )}
              <div className="flex items-center gap-2">
                <Switch checked={editEnabled} onCheckedChange={setEditEnabled} id="edit-enabled" />
                <Label htmlFor="edit-enabled">Enabled</Label>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingSchedule(null)}>
              Cancel
            </Button>
            <Button onClick={handleUpdate} disabled={updateSchedule.isPending}>
              {updateSchedule.isPending ? "Saving..." : "Save"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Confirm */}
      <ConfirmDialog
        open={!!deletingId}
        onOpenChange={(open) => { if (!open) setDeletingId(null); }}
        title="Delete Schedule"
        description="This will permanently remove the schedule and unregister it from the scheduler."
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteSchedule.isPending}
        onConfirm={handleDelete}
      />
    </>
  );
}
