"use client";

import {
  Briefcase,
  Cable,
  Eye,
  KeyRound,
  Shield,
  Activity,
  CheckCircle2,
  XCircle,
} from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useDashboardSummary } from "@/lib/hooks/use-dashboard";

export function SummaryCards() {
  const { data, isLoading } = useDashboardSummary();
  const summary = data?.data;

  if (isLoading) {
    return (
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {Array.from({ length: 6 }).map((_, i) => (
          <Card key={i}>
            <CardContent className="p-4">
              <Skeleton className="mb-2 h-4 w-20" />
              <Skeleton className="h-8 w-12" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  if (!summary) return null;

  const cards = [
    {
      label: "Jobs",
      value: summary.totalJobs,
      icon: Briefcase,
      color: "text-blue-600",
      bg: "bg-blue-50",
    },
    {
      label: "Connections",
      value: summary.totalConnections,
      icon: Cable,
      color: "text-violet-600",
      bg: "bg-violet-50",
    },
    {
      label: "Monitors",
      value: summary.totalMonitors,
      icon: Eye,
      color: "text-amber-600",
      bg: "bg-amber-50",
    },
    {
      label: "PGP Keys",
      value: summary.totalPgpKeys,
      icon: Shield,
      color: "text-emerald-600",
      bg: "bg-emerald-50",
    },
    {
      label: "SSH Keys",
      value: summary.totalSshKeys,
      icon: KeyRound,
      color: "text-cyan-600",
      bg: "bg-cyan-50",
    },
    {
      label: "Last 24h",
      value: summary.executions24H,
      icon: Activity,
      color: "text-slate-600",
      bg: "bg-slate-50",
      sub: (
        <div className="mt-1 flex items-center gap-2 text-xs">
          <span className="flex items-center gap-0.5 text-emerald-600">
            <CheckCircle2 className="h-3 w-3" />
            {summary.executionsSucceeded24H}
          </span>
          <span className="flex items-center gap-0.5 text-red-500">
            <XCircle className="h-3 w-3" />
            {summary.executionsFailed24H}
          </span>
        </div>
      ),
    },
  ];

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
      {cards.map((card) => (
        <Card key={card.label}>
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <span className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                {card.label}
              </span>
              <div className={`rounded-md p-1.5 ${card.bg}`}>
                <card.icon className={`h-3.5 w-3.5 ${card.color}`} />
              </div>
            </div>
            <p className="mt-1 text-2xl font-semibold tabular-nums tracking-tight">
              {card.value}
            </p>
            {card.sub}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
