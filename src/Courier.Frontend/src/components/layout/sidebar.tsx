"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  Briefcase,
  Cable,
  KeyRound,
  Eye,
  FileText,
  ChevronLeft,
  ChevronRight,
  Package,
  Tag,
  Link2,
  Bell,
  ShieldCheck,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { usePermissions } from "@/lib/hooks/use-permissions";

const navItems = [
  { label: "Dashboard", href: "/", icon: LayoutDashboard, active: true, exact: true },
  { label: "Jobs", href: "/jobs", icon: Briefcase, active: true },
  { label: "Chains", href: "/chains", icon: Link2, active: true },
  { label: "Connections", href: "/connections", icon: Cable, active: true },
  { label: "Keys", href: "/keys", icon: KeyRound, active: true },
  { label: "Monitors", href: "/monitors", icon: Eye, active: true },
  { label: "Tags", href: "/tags", icon: Tag, active: true },
  { label: "Notifications", href: "/notifications", icon: Bell, active: true },
  { label: "Audit", href: "/audit", icon: FileText, active: true },
];


export function Sidebar() {
  const pathname = usePathname();
  const [collapsed, setCollapsed] = useState(false);
  const { canAny } = usePermissions();

  return (
    <aside
      className={cn(
        "flex flex-col border-r border-sidebar-border bg-sidebar-background transition-all duration-200",
        collapsed ? "w-16" : "w-56"
      )}
    >
      <div className="flex h-14 items-center border-b border-sidebar-border px-4">
        {!collapsed && (
          <div className="flex items-center gap-2.5">
            <Package className="h-5 w-5 text-sidebar-primary" />
            <span className="text-sm font-bold tracking-widest uppercase text-sidebar-primary">
              Courier
            </span>
          </div>
        )}
        {collapsed && <Package className="mx-auto h-5 w-5 text-sidebar-primary" />}
      </div>

      <nav className="flex-1 space-y-0.5 p-2 pt-3">
        {navItems.map((item) => {
          const isActive = item.active && ("exact" in item && item.exact ? pathname === item.href : pathname.startsWith(item.href));
          return (
            <Link
              key={item.label}
              href={item.active ? item.href : "#"}
              className={cn(
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                isActive
                  ? "bg-sidebar-accent text-sidebar-primary"
                  : item.active
                    ? "text-sidebar-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground"
                    : "cursor-not-allowed text-sidebar-foreground/30"
              )}
              title={!item.active ? "Coming Soon" : undefined}
              onClick={!item.active ? (e) => e.preventDefault() : undefined}
            >
              <item.icon className="h-4 w-4 shrink-0" />
              {!collapsed && <span>{item.label}</span>}
              {!collapsed && !item.active && (
                <span className="ml-auto rounded bg-sidebar-border/60 px-1.5 py-0.5 text-[10px] font-medium text-sidebar-foreground/40">
                  Soon
                </span>
              )}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-sidebar-border p-2">
        {canAny("UsersView", "AuthProvidersView", "SettingsView") && (
          <Link
            href="/admin"
            className={cn(
              "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
              pathname.startsWith("/admin")
                ? "bg-sidebar-accent text-sidebar-primary"
                : "text-sidebar-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground"
            )}
          >
            <ShieldCheck className="h-4 w-4 shrink-0" />
            {!collapsed && <span>Admin</span>}
          </Link>
        )}

        <Button
          variant="ghost"
          size="sm"
          className="mt-2 w-full text-sidebar-foreground/50 hover:text-sidebar-foreground hover:bg-sidebar-accent/60"
          onClick={() => setCollapsed(!collapsed)}
        >
          {collapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
          {!collapsed && <span className="ml-2">Collapse</span>}
        </Button>
      </div>
    </aside>
  );
}
