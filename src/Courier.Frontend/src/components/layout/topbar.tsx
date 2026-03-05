"use client";

import { usePathname } from "next/navigation";
import Link from "next/link";
import { ChevronRight, LogOut, User, KeyRound } from "lucide-react";
import { useAuth } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

function useBreadcrumbs() {
  const pathname = usePathname();
  const segments = pathname.split("/").filter(Boolean);

  const crumbs: { label: string; href: string }[] = [];

  for (let i = 0; i < segments.length; i++) {
    const href = "/" + segments.slice(0, i + 1).join("/");
    let label = segments[i];

    if (label === "jobs") label = "Jobs";
    else if (label === "new") label = "Create";
    else if (label === "edit") label = "Edit";
    else if (label === "connections") label = "Connections";
    else if (label === "keys") label = "Keys";
    else if (label === "monitors") label = "Monitors";
    else if (label === "chains") label = "Chains";
    else if (label === "tags") label = "Tags";
    else if (label === "notifications") label = "Notifications";
    else if (label === "audit") label = "Audit";
    else if (label === "settings") label = "Settings";
    else if (label === "users") label = "Users";
    else if (label === "pgp") label = "PGP";
    else if (label === "ssh") label = "SSH";
    else if (label === "logs") label = "Logs";
    else if (label === "import") label = "Import";
    else if (label.match(/^[0-9a-f-]{36}$/)) label = "Detail";

    crumbs.push({ label, href });
  }

  return crumbs;
}

export function Topbar() {
  const crumbs = useBreadcrumbs();
  const { user, logout } = useAuth();

  const initials = user?.displayName
    ?.split(" ")
    .map((n) => n[0])
    .join("")
    .toUpperCase()
    .slice(0, 2) ?? "?";

  return (
    <header className="flex h-12 items-center justify-between border-b bg-background px-6">
      <nav className="flex items-center gap-1 text-sm text-muted-foreground">
        {crumbs.map((crumb, i) => (
          <span key={crumb.href} className="flex items-center gap-1">
            {i > 0 && <ChevronRight className="h-3 w-3" />}
            {i < crumbs.length - 1 ? (
              <Link href={crumb.href} className="hover:text-foreground transition-colors">
                {crumb.label}
              </Link>
            ) : (
              <span className="text-foreground font-medium">{crumb.label}</span>
            )}
          </span>
        ))}
      </nav>

      {user && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="gap-2">
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-primary text-xs font-medium text-primary-foreground">
                {initials}
              </div>
              <span className="text-sm">{user.displayName}</span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48">
            <div className="px-2 py-1.5 text-xs text-muted-foreground">
              {user.username} &middot; {user.role}
            </div>
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild>
              <Link href="/settings/users">
                <User className="mr-2 h-4 w-4" />
                Profile
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem asChild>
              <Link href="/settings">
                <KeyRound className="mr-2 h-4 w-4" />
                Change Password
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => logout()}>
              <LogOut className="mr-2 h-4 w-4" />
              Sign Out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </header>
  );
}
