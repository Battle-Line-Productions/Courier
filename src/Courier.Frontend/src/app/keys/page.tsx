"use client";

import { useState } from "react";
import Link from "next/link";
import { Plus, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { PgpKeyTable } from "@/components/keys/pgp-key-table";
import { SshKeyTable } from "@/components/keys/ssh-key-table";
import { EmptyState } from "@/components/shared/empty-state";
import { usePgpKeys } from "@/lib/hooks/use-pgp-keys";
import { useSshKeys } from "@/lib/hooks/use-ssh-keys";

export default function KeysPage() {
  const [tab, setTab] = useState("pgp");

  // PGP state
  const [pgpPage, setPgpPage] = useState(1);
  const [pgpSearch, setPgpSearch] = useState("");
  const [pgpStatus, setPgpStatus] = useState("");
  const pgpFilters = { search: pgpSearch || undefined, status: pgpStatus || undefined };
  const { data: pgpData, isLoading: pgpLoading } = usePgpKeys(pgpPage, 10, pgpFilters);
  const pgpKeys = pgpData?.data ?? [];
  const pgpPagination = pgpData?.pagination;

  // SSH state
  const [sshPage, setSshPage] = useState(1);
  const [sshSearch, setSshSearch] = useState("");
  const [sshStatus, setSshStatus] = useState("");
  const sshFilters = { search: sshSearch || undefined, status: sshStatus || undefined };
  const { data: sshData, isLoading: sshLoading } = useSshKeys(sshPage, 10, sshFilters);
  const sshKeys = sshData?.data ?? [];
  const sshPagination = sshData?.pagination;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Keys</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Manage PGP and SSH keys for encryption and authentication
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" asChild>
            <Link href={tab === "pgp" ? "/keys/pgp/import" : "/keys/ssh/import"}>
              <Upload className="mr-2 h-4 w-4" /> Import
            </Link>
          </Button>
          <Button asChild>
            <Link href={tab === "pgp" ? "/keys/pgp/new" : "/keys/ssh/new"}>
              <Plus className="mr-2 h-4 w-4" /> Generate
            </Link>
          </Button>
        </div>
      </div>

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="pgp">PGP Keys</TabsTrigger>
          <TabsTrigger value="ssh">SSH Keys</TabsTrigger>
        </TabsList>

        <TabsContent value="pgp" className="space-y-4 mt-4">
          {pgpLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-64 w-full" />
            </div>
          ) : pgpKeys.length === 0 && !pgpSearch && !pgpStatus ? (
            <EmptyState
              title="No PGP keys yet"
              description="Generate or import your first PGP key for encryption and signing operations."
              actionLabel="Generate PGP Key"
              actionHref="/keys/pgp/new"
            />
          ) : (
            <>
              <div className="flex items-center gap-3">
                <Input
                  placeholder="Search PGP keys..."
                  value={pgpSearch}
                  onChange={(e) => { setPgpSearch(e.target.value); setPgpPage(1); }}
                  className="max-w-sm"
                />
                <Select value={pgpStatus} onValueChange={(v) => { setPgpStatus(v === "all" ? "" : v); setPgpPage(1); }}>
                  <SelectTrigger className="w-36"><SelectValue placeholder="Status" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Statuses</SelectItem>
                    <SelectItem value="active">Active</SelectItem>
                    <SelectItem value="expiring">Expiring</SelectItem>
                    <SelectItem value="retired">Retired</SelectItem>
                    <SelectItem value="revoked">Revoked</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              {pgpKeys.length === 0 ? (
                <p className="text-sm text-muted-foreground py-8 text-center">No PGP keys match your filters.</p>
              ) : (
                <PgpKeyTable keys={pgpKeys} />
              )}
              {pgpPagination && pgpPagination.totalPages > 1 && (
                <div className="flex items-center justify-center gap-2">
                  <Button variant="outline" size="sm" disabled={pgpPage <= 1} onClick={() => setPgpPage((p) => p - 1)}>Previous</Button>
                  <span className="text-sm text-muted-foreground tabular-nums">Page {pgpPagination.page} of {pgpPagination.totalPages}</span>
                  <Button variant="outline" size="sm" disabled={pgpPage >= pgpPagination.totalPages} onClick={() => setPgpPage((p) => p + 1)}>Next</Button>
                </div>
              )}
            </>
          )}
        </TabsContent>

        <TabsContent value="ssh" className="space-y-4 mt-4">
          {sshLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-64 w-full" />
            </div>
          ) : sshKeys.length === 0 && !sshSearch && !sshStatus ? (
            <EmptyState
              title="No SSH keys yet"
              description="Generate or import your first SSH key for SFTP authentication."
              actionLabel="Generate SSH Key"
              actionHref="/keys/ssh/new"
            />
          ) : (
            <>
              <div className="flex items-center gap-3">
                <Input
                  placeholder="Search SSH keys..."
                  value={sshSearch}
                  onChange={(e) => { setSshSearch(e.target.value); setSshPage(1); }}
                  className="max-w-sm"
                />
                <Select value={sshStatus} onValueChange={(v) => { setSshStatus(v === "all" ? "" : v); setSshPage(1); }}>
                  <SelectTrigger className="w-36"><SelectValue placeholder="Status" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Statuses</SelectItem>
                    <SelectItem value="active">Active</SelectItem>
                    <SelectItem value="retired">Retired</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              {sshKeys.length === 0 ? (
                <p className="text-sm text-muted-foreground py-8 text-center">No SSH keys match your filters.</p>
              ) : (
                <SshKeyTable keys={sshKeys} />
              )}
              {sshPagination && sshPagination.totalPages > 1 && (
                <div className="flex items-center justify-center gap-2">
                  <Button variant="outline" size="sm" disabled={sshPage <= 1} onClick={() => setSshPage((p) => p - 1)}>Previous</Button>
                  <span className="text-sm text-muted-foreground tabular-nums">Page {sshPagination.page} of {sshPagination.totalPages}</span>
                  <Button variant="outline" size="sm" disabled={sshPage >= sshPagination.totalPages} onClick={() => setSshPage((p) => p + 1)}>Next</Button>
                </div>
              )}
            </>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
