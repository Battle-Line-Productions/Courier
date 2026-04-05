"use client";

import { useState, useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useUser, useUpdateUser, useResetUserPassword } from "@/lib/hooks/use-users";
import { useAuth } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";

export default function UserDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;
  const { data, isLoading } = useUser(id);
  const updateUser = useUpdateUser(id);
  const resetPassword = useResetUserPassword(id);
  const { user: currentUser } = useAuth();

  const userData = data?.data;

  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("viewer");
  const [isActive, setIsActive] = useState(true);
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    if (userData && !initialized) {
      setDisplayName(userData.displayName);
      setEmail(userData.email ?? "");
      setRole(userData.role);
      setIsActive(userData.isActive);
      setInitialized(true);
    }
  }, [userData, initialized]);

  if (isLoading) return <div className="text-sm text-muted-foreground">Loading user...</div>;
  if (!userData) return <div className="text-sm text-muted-foreground">User not found.</div>;

  async function handleUpdate(e: React.FormEvent) {
    e.preventDefault();
    try {
      await updateUser.mutateAsync({
        displayName,
        email: email || undefined,
        role,
        isActive,
      });
      toast.success("User updated successfully.");
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to update user.");
    }
  }

  async function handleResetPassword(e: React.FormEvent) {
    e.preventDefault();
    if (newPassword !== confirmNewPassword) {
      toast.error("Passwords do not match.");
      return;
    }
    try {
      await resetPassword.mutateAsync({ password: newPassword, confirmPassword: confirmNewPassword });
      toast.success("Password reset successfully.");
      setNewPassword("");
      setConfirmNewPassword("");
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to reset password.");
    }
  }

  const isSelf = currentUser?.id === id;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{userData.username}</h1>
        <p className="text-sm text-muted-foreground">
          Created {new Date(userData.createdAt).toLocaleDateString()}
          {userData.lastLoginAt && ` · Last login ${new Date(userData.lastLoginAt).toLocaleString()}`}
        </p>
      </div>

      <form onSubmit={handleUpdate} className="max-w-lg space-y-4">
        <h2 className="text-lg font-medium">Account Details</h2>

        <div className="space-y-2">
          <Label htmlFor="displayName">Display Name</Label>
          <Input id="displayName" value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
        </div>

        <div className="space-y-2">
          <Label htmlFor="email">Email</Label>
          <Input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>

        <div className="space-y-2">
          <Label htmlFor="role">Role</Label>
          <select
            id="role"
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            value={role}
            onChange={(e) => setRole(e.target.value)}
            disabled={isSelf}
          >
            <option value="admin">Admin</option>
            <option value="operator">Operator</option>
            <option value="viewer">Viewer</option>
          </select>
          {isSelf && <p className="text-xs text-muted-foreground">You cannot change your own role.</p>}
        </div>

        <div className="flex items-center gap-2">
          <input
            type="checkbox"
            id="isActive"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
            disabled={isSelf}
            className="h-4 w-4 rounded border-input"
          />
          <Label htmlFor="isActive">Account Active</Label>
        </div>

        <div className="flex gap-3">
          <Button type="submit" disabled={updateUser.isPending}>
            {updateUser.isPending ? "Saving..." : "Save Changes"}
          </Button>
          <Button type="button" variant="outline" onClick={() => router.push("/admin")}>
            Back
          </Button>
        </div>
      </form>

      {!isSelf && (
        <form onSubmit={handleResetPassword} className="max-w-lg space-y-4 border-t pt-6">
          <h2 className="text-lg font-medium">Reset Password</h2>
          <p className="text-sm text-muted-foreground">Set a new password for this user. All their active sessions will be revoked.</p>

          <div className="space-y-2">
            <Label htmlFor="newPassword">New Password</Label>
            <Input id="newPassword" type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} required />
          </div>

          <div className="space-y-2">
            <Label htmlFor="confirmNewPassword">Confirm New Password</Label>
            <Input id="confirmNewPassword" type="password" value={confirmNewPassword} onChange={(e) => setConfirmNewPassword(e.target.value)} required />
          </div>

          <Button type="submit" variant="destructive" disabled={resetPassword.isPending}>
            {resetPassword.isPending ? "Resetting..." : "Reset Password"}
          </Button>
        </form>
      )}
    </div>
  );
}
