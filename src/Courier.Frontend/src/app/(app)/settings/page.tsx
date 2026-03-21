"use client";

import { useState } from "react";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuthSettings, useUpdateAuthSettings } from "@/lib/hooks/use-settings";
import { useChangePassword } from "@/lib/hooks/use-auth-actions";
import { useAuth } from "@/lib/auth";
import { usePermissions } from "@/lib/hooks/use-permissions";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";
import { KeyRound, Lock } from "lucide-react";

function AuthSettingsTab() {
  const { data, isLoading } = useAuthSettings();
  const updateSettings = useUpdateAuthSettings();
  const settings = data?.data;

  const [form, setForm] = useState<{
    sessionTimeoutMinutes: number;
    refreshTokenDays: number;
    passwordMinLength: number;
    maxLoginAttempts: number;
    lockoutDurationMinutes: number;
  } | null>(null);

  // Initialize form when data loads
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
      toast.success("Authentication settings updated.");
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
            />
          </div>
        </div>
      </div>

      <Button onClick={handleSave} disabled={updateSettings.isPending}>
        {updateSettings.isPending ? "Saving..." : "Save Changes"}
      </Button>
    </div>
  );
}

function ChangePasswordTab() {
  const changePassword = useChangePassword();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (newPassword !== confirmNewPassword) {
      toast.error("Passwords do not match.");
      return;
    }
    try {
      await changePassword.mutateAsync({ currentPassword, newPassword, confirmNewPassword });
      toast.success("Password changed successfully.");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmNewPassword("");
    } catch (err) {
      if (err instanceof ApiClientError) {
        toast.error(err.message);
      } else {
        toast.error("Failed to change password.");
      }
    }
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-md space-y-4">
      <div className="space-y-2">
        <Label htmlFor="currentPassword">Current Password</Label>
        <Input
          id="currentPassword"
          type="password"
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)}
          required
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="newPassword">New Password</Label>
        <Input
          id="newPassword"
          type="password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          required
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="confirmNewPassword">Confirm New Password</Label>
        <Input
          id="confirmNewPassword"
          type="password"
          value={confirmNewPassword}
          onChange={(e) => setConfirmNewPassword(e.target.value)}
          required
        />
      </div>
      <Button type="submit" disabled={changePassword.isPending}>
        {changePassword.isPending ? "Changing..." : "Change Password"}
      </Button>
    </form>
  );
}


export default function SettingsPage() {
  const { user } = useAuth();
  const { can } = usePermissions();
  const isAdmin = can("SettingsManage");

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Settings</h1>
        <p className="text-sm text-muted-foreground">Manage application configuration.</p>
      </div>

      <Tabs defaultValue={isAdmin ? "auth" : "password"}>
        <TabsList>
          {isAdmin && <TabsTrigger value="auth"><Lock className="mr-1.5 h-3.5 w-3.5" />Authentication</TabsTrigger>}
          <TabsTrigger value="password"><KeyRound className="mr-1.5 h-3.5 w-3.5" />Change Password</TabsTrigger>
        </TabsList>
        {isAdmin && (
          <TabsContent value="auth" className="mt-6">
            <AuthSettingsTab />
          </TabsContent>
        )}
        <TabsContent value="password" className="mt-6">
          <ChangePasswordTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}
