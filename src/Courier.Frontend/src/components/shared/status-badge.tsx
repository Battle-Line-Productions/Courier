import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

const stateStyles: Record<string, string> = {
  created: "bg-gray-100 text-gray-600 border-gray-200",
  queued: "bg-amber-50 text-amber-700 border-amber-200",
  running: "bg-blue-50 text-blue-700 border-blue-200",
  completed: "bg-emerald-50 text-emerald-700 border-emerald-200",
  failed: "bg-red-50 text-red-700 border-red-200",
  cancelled: "bg-gray-50 text-gray-400 border-gray-200",
  pending: "bg-gray-100 text-gray-600 border-gray-200",
  skipped: "bg-gray-50 text-gray-400 border-gray-200",
  active: "bg-emerald-50 text-emerald-700 border-emerald-200",
  disabled: "bg-gray-50 text-gray-400 border-gray-200",
  expiring: "bg-amber-50 text-amber-700 border-amber-200",
  retired: "bg-gray-100 text-gray-500 border-gray-200",
  revoked: "bg-red-50 text-red-700 border-red-200",
  deleted: "bg-red-50 text-red-400 border-red-200",
};

const stateIcons: Record<string, string> = {
  created: "\u25cb",
  queued: "\u25cb",
  running: "\u25cf",
  completed: "\u2713",
  failed: "\u2717",
  cancelled: "\u2298",
  pending: "\u25cb",
  skipped: "\u2014",
  active: "\u2713",
  disabled: "\u25cb",
  expiring: "\u26a0",
  retired: "\u25cb",
  revoked: "\u2717",
  deleted: "\u2717",
};

interface StatusBadgeProps {
  state: string;
  className?: string;
  pulse?: boolean;
}

export function StatusBadge({ state, className, pulse }: StatusBadgeProps) {
  const normalized = state.toLowerCase();
  return (
    <Badge
      variant="outline"
      className={cn(
        "gap-1.5 font-medium text-xs",
        stateStyles[normalized] || "bg-gray-100 text-gray-600",
        pulse && normalized === "running" && "animate-pulse",
        className
      )}
    >
      <span>{stateIcons[normalized] || "\u25cf"}</span>
      <span className="capitalize">{normalized}</span>
    </Badge>
  );
}
