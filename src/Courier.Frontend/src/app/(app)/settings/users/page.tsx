"use client";

import { useState } from "react";
import Link from "next/link";
import { useUsers, useDeleteUser } from "@/lib/hooks/use-users";
import { useAuth } from "@/lib/auth";
import { usePermissions } from "@/lib/hooks/use-permissions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Search, Trash2, UserPlus } from "lucide-react";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";
import { cn } from "@/lib/utils";

const roleBadgeColors: Record<string, string> = {
  admin: "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400",
  operator: "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400",
  viewer: "bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400",
};

export default function UsersPage() {
  const { user: currentUser } = useAuth();
  const { can } = usePermissions();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const { data, isLoading } = useUsers(page, 10, search || undefined);
  const deleteUser = useDeleteUser();

  if (!can("UsersManage")) {
    return (
      <div className="text-center text-muted-foreground py-12">
        You do not have permission to view this page.
      </div>
    );
  }

  const users = data?.data ?? [];
  const pagination = data?.pagination;

  async function handleDelete(userId: string, username: string) {
    if (!confirm(`Are you sure you want to delete user "${username}"?`)) return;
    try {
      await deleteUser.mutateAsync(userId);
      toast.success(`User "${username}" deleted.`);
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to delete user.");
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Users</h1>
          <p className="text-sm text-muted-foreground">Manage user accounts and roles.</p>
        </div>
        {can("UsersManage") && (
          <Button asChild>
            <Link href="/settings/users/new">
              <UserPlus className="mr-2 h-4 w-4" />
              Add User
            </Link>
          </Button>
        )}
      </div>

      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search users..."
          className="pl-9"
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
        />
      </div>

      {isLoading ? (
        <div className="text-sm text-muted-foreground">Loading users...</div>
      ) : users.length === 0 ? (
        <div className="text-center text-muted-foreground py-12">No users found.</div>
      ) : (
        <>
          <div className="rounded-md border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/50">
                  <th className="px-4 py-2 text-left font-medium">Username</th>
                  <th className="px-4 py-2 text-left font-medium">Display Name</th>
                  <th className="px-4 py-2 text-left font-medium">Role</th>
                  <th className="px-4 py-2 text-left font-medium">Status</th>
                  <th className="px-4 py-2 text-left font-medium">Last Login</th>
                  <th className="px-4 py-2 text-right font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id} className="border-b last:border-0">
                    <td className="px-4 py-2">
                      <Link href={`/settings/users/${u.id}`} className="font-medium text-primary hover:underline">
                        {u.username}
                      </Link>
                    </td>
                    <td className="px-4 py-2 text-muted-foreground">{u.displayName}</td>
                    <td className="px-4 py-2">
                      <span className={cn("inline-block rounded-full px-2 py-0.5 text-xs font-medium capitalize", roleBadgeColors[u.role] ?? "bg-gray-100")}>
                        {u.role}
                      </span>
                    </td>
                    <td className="px-4 py-2">
                      {u.isActive ? (
                        <span className="text-green-600 dark:text-green-400">Active</span>
                      ) : (
                        <span className="text-red-600 dark:text-red-400">Disabled</span>
                      )}
                    </td>
                    <td className="px-4 py-2 text-muted-foreground text-xs">
                      {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "Never"}
                    </td>
                    <td className="px-4 py-2 text-right">
                      {u.id !== currentUser?.id && (
                        <Button variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive" onClick={() => handleDelete(u.id, u.username)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {pagination && pagination.totalPages > 1 && (
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">
                Page {pagination.page} of {pagination.totalPages} ({pagination.totalCount} total)
              </span>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={pagination.page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
                <Button variant="outline" size="sm" disabled={pagination.page >= pagination.totalPages} onClick={() => setPage(page + 1)}>Next</Button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
