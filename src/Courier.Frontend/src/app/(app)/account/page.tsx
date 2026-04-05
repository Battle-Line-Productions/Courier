"use client";

import { useState } from "react";
import { useAuth } from "@/lib/auth";
import { useChangePassword } from "@/lib/hooks/use-auth-actions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Info } from "lucide-react";

const roleBadgeColors: Record<string, string> = {
  admin: "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400",
  operator: "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400",
  viewer: "bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400",
};

function ChangePasswordForm() {
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

export default function AccountPage() {
  const { user } = useAuth();

  if (!user) return null;

  const showChangePassword = !user.isSsoUser || user.allowLocalPassword;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">My Account</h1>
        <p className="text-sm text-muted-foreground">View your account details.</p>
      </div>

      <div className="max-w-lg space-y-4">
        <h2 className="text-lg font-medium">Profile</h2>

        <div className="rounded-md border divide-y">
          <div className="flex justify-between px-4 py-3">
            <span className="text-sm text-muted-foreground">Display Name</span>
            <span className="text-sm font-medium">{user.displayName}</span>
          </div>
          <div className="flex justify-between px-4 py-3">
            <span className="text-sm text-muted-foreground">Username</span>
            <span className="text-sm font-medium">{user.username}</span>
          </div>
          <div className="flex justify-between px-4 py-3">
            <span className="text-sm text-muted-foreground">Email</span>
            <span className="text-sm font-medium">{user.email ?? "—"}</span>
          </div>
          <div className="flex justify-between px-4 py-3">
            <span className="text-sm text-muted-foreground">Role</span>
            <span className={cn("inline-block rounded-full px-2 py-0.5 text-xs font-medium capitalize", roleBadgeColors[user.role] ?? "bg-gray-100")}>
              {user.role}
            </span>
          </div>
          {user.isSsoUser && user.ssoProviderName && (
            <div className="flex justify-between px-4 py-3">
              <span className="text-sm text-muted-foreground">SSO Provider</span>
              <span className="text-sm font-medium">{user.ssoProviderName}</span>
            </div>
          )}
          {user.lastLoginAt && (
            <div className="flex justify-between px-4 py-3">
              <span className="text-sm text-muted-foreground">Last Login</span>
              <span className="text-sm font-medium">{new Date(user.lastLoginAt).toLocaleString()}</span>
            </div>
          )}
        </div>
      </div>

      <div className="max-w-lg space-y-4 border-t pt-6">
        <h2 className="text-lg font-medium">Change Password</h2>

        {showChangePassword ? (
          <ChangePasswordForm />
        ) : (
          <div className="flex items-start gap-3 rounded-md border border-blue-200 bg-blue-50 p-4 dark:border-blue-800 dark:bg-blue-950">
            <Info className="h-5 w-5 text-blue-600 dark:text-blue-400 mt-0.5 shrink-0" />
            <p className="text-sm text-blue-800 dark:text-blue-200">
              Your password is managed by <strong>{user.ssoProviderName}</strong>. Contact your identity provider administrator to change your password.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
