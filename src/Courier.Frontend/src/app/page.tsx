"use client";

import { SummaryCards } from "@/components/dashboard/summary-cards";
import { RecentExecutionsTable } from "@/components/dashboard/recent-executions-table";
import { ActiveMonitorsList } from "@/components/dashboard/active-monitors-list";
import { KeyExpiryList } from "@/components/dashboard/key-expiry-list";

export default function DashboardPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Dashboard</h1>
        <p className="text-sm text-muted-foreground">
          System overview and recent activity.
        </p>
      </div>

      <SummaryCards />

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <RecentExecutionsTable />
        </div>
        <div>
          <ActiveMonitorsList />
        </div>
      </div>

      <KeyExpiryList />
    </div>
  );
}
