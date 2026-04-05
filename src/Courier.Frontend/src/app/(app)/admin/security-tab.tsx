"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuthSettings, useUpdateAuthSettings } from "@/lib/hooks/use-settings";
import { usePermissions } from "@/lib/hooks/use-permissions";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";

export function SecurityTab() {
  const { data, isLoading } = useAuthSettings();
  const updateSettings = useUpdateAuthSettings();
  const { can } = usePermissions();
  const settings = data?.data;
  const canEdit = can("SettingsManage");

  const [form, setForm] = useState<{
    sessionTimeoutMinutes: number;
    refreshTokenDays: number;
    passwordMinLength: number;
    maxLoginAttempts: number;
    lockoutDurationMinutes: number;
  } | null>(null);

  const currentForm = form ?? (settings ? {
    sessionTimeoutMinutes: settings.sessionTimeoutMinutes,
    refreshTokenDays: settings.refreshTokenDays,
    passwordMinLength: settings.passwordMinLength,
    maxLoginAttempts: settings.maxLoginAttempts,
    lockoutDurationMinutes: settings.lockoutDurationMinutes,
  } : null);

  if (isLoading || !currentForm) {
    return <div className="text-sm text-muted-foreground">Loading settings...</div>;
  }

  async function handleSave() {
    if (!currentForm) return;
    try {
      await updateSettings.mutateAsync(currentForm);
      toast.success("Security settings updated.");
    } catch (err) {
      if (err instanceof ApiClientError) {
        toast.error(err.message);
      } else {
        toast.error("Failed to update settings.");
      }
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-sm font-medium">Session</h3>
        <p className="text-xs text-muted-foreground mb-3">Configure token lifetimes for user sessions.</p>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="sessionTimeout">Access Token Lifetime (minutes)</Label>
            <Input
              id="sessionTimeout"
              type="number"
              min={1}
              value={currentForm.sessionTimeoutMinutes}
              onChange={(e) => setForm({ ...currentForm, sessionTimeoutMinutes: parseInt(e.target.value) || 1 })}
              disabled={!canEdit}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="refreshDays">Refresh Token Lifetime (days)</Label>
            <Input
              id="refreshDays"
              type="number"
              min={1}
              value={currentForm.refreshTokenDays}
              onChange={(e) => setForm({ ...currentForm, refreshTokenDays: parseInt(e.target.value) || 1 })}
              disabled={!canEdit}
            />
          </div>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium">Password Policy</h3>
        <p className="text-xs text-muted-foreground mb-3">Set minimum password requirements.</p>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="minLength">Minimum Length</Label>
            <Input
              id="minLength"
              type="number"
              min={4}
              value={currentForm.passwordMinLength}
              onChange={(e) => setForm({ ...currentForm, passwordMinLength: parseInt(e.target.value) || 4 })}
              disabled={!canEdit}
            />
          </div>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium">Account Lockout</h3>
        <p className="text-xs text-muted-foreground mb-3">Protect against brute-force attacks.</p>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="maxAttempts">Max Failed Attempts</Label>
            <Input
              id="maxAttempts"
              type="number"
              min={1}
              value={currentForm.maxLoginAttempts}
              onChange={(e) => setForm({ ...currentForm, maxLoginAttempts: parseInt(e.target.value) || 1 })}
              disabled={!canEdit}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="lockoutDuration">Lockout Duration (minutes)</Label>
            <Input
              id="lockoutDuration"
              type="number"
              min={1}
              value={currentForm.lockoutDurationMinutes}
              onChange={(e) => setForm({ ...currentForm, lockoutDurationMinutes: parseInt(e.target.value) || 1 })}
              disabled={!canEdit}
            />
          </div>
        </div>
      </div>

      {canEdit && (
        <Button onClick={handleSave} disabled={updateSettings.isPending}>
          {updateSettings.isPending ? "Saving..." : "Save Changes"}
        </Button>
      )}
    </div>
  );
}
