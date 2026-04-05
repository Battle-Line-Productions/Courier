"use client";

import { useState } from "react";
import Link from "next/link";
import { useAuthProviders, useDeleteAuthProvider } from "@/lib/hooks/use-auth-providers";
import { usePermissions } from "@/lib/hooks/use-permissions";
import { Button } from "@/components/ui/button";
import { Trash2, PlusCircle, Pencil } from "lucide-react";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";
import { cn } from "@/lib/utils";

const typeBadgeColors: Record<string, string> = {
  oidc: "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400",
  saml: "bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400",
};

export function AuthProvidersTab() {
  const { can } = usePermissions();
  const [page, setPage] = useState(1);
  const { data, isLoading } = useAuthProviders(page, 25);
  const deleteAuthProvider = useDeleteAuthProvider();

  const providers = data?.data ?? [];
  const pagination = data?.pagination;

  async function handleDelete(id: string, name: string) {
    if (!confirm(`Are you sure you want to delete auth provider "${name}"?`)) return;
    try {
      await deleteAuthProvider.mutateAsync(id);
      toast.success(`Auth provider "${name}" deleted.`);
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to delete auth provider.");
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-end">
        {can("AuthProvidersCreate") && (
          <Button asChild>
            <Link href="/admin/auth-providers/new">
              <PlusCircle className="mr-2 h-4 w-4" />
              Add Provider
            </Link>
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="text-sm text-muted-foreground">Loading auth providers...</div>
      ) : providers.length === 0 ? (
        <div className="text-center text-muted-foreground py-12">No auth providers configured.</div>
      ) : (
        <>
          <div className="rounded-md border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/50">
                  <th className="px-4 py-2 text-left font-medium">Name</th>
                  <th className="px-4 py-2 text-left font-medium">Type</th>
                  <th className="px-4 py-2 text-left font-medium">Enabled</th>
                  <th className="px-4 py-2 text-left font-medium">Linked Users</th>
                  <th className="px-4 py-2 text-right font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {providers.map((p) => (
                  <tr key={p.id} className="border-b last:border-0">
                    <td className="px-4 py-2">
                      <Link
                        href={`/admin/auth-providers/${p.id}`}
                        className="font-medium text-primary hover:underline"
                      >
                        {p.name}
                      </Link>
                    </td>
                    <td className="px-4 py-2">
                      <span
                        className={cn(
                          "inline-block rounded-full px-2 py-0.5 text-xs font-medium uppercase",
                          typeBadgeColors[p.type] ?? "bg-gray-100"
                        )}
                      >
                        {p.type}
                      </span>
                    </td>
                    <td className="px-4 py-2">
                      {p.isEnabled ? (
                        <span className="text-green-600 dark:text-green-400">Enabled</span>
                      ) : (
                        <span className="text-muted-foreground">Disabled</span>
                      )}
                    </td>
                    <td className="px-4 py-2 text-muted-foreground">{p.linkedUserCount}</td>
                    <td className="px-4 py-2 text-right flex justify-end gap-1">
                      {can("AuthProvidersEdit") && (
                        <Button variant="ghost" size="icon" className="h-8 w-8" asChild>
                          <Link href={`/admin/auth-providers/${p.id}`}>
                            <Pencil className="h-4 w-4" />
                          </Link>
                        </Button>
                      )}
                      {can("AuthProvidersDelete") && (
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-destructive hover:text-destructive"
                          onClick={() => handleDelete(p.id, p.name)}
                        >
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
                <Button
                  variant="outline"
                  size="sm"
                  disabled={pagination.page <= 1}
                  onClick={() => setPage(page - 1)}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={pagination.page >= pagination.totalPages}
                  onClick={() => setPage(page + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
