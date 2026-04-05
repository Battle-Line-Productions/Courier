"use client";

import { usePermissions } from "@/lib/hooks/use-permissions";
import type { Permission } from "@/lib/permissions";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Users, ShieldCheck, Lock, Mail } from "lucide-react";
import { UsersTab } from "./users-tab";
import { AuthProvidersTab } from "./auth-providers-tab";
import { SecurityTab } from "./security-tab";
import { EmailTab } from "./email-tab";

interface AdminTab {
  key: string;
  label: string;
  permission: Permission;
  icon: React.ComponentType<{ className?: string }>;
  component: React.ComponentType;
}

const adminTabs: AdminTab[] = [
  { key: "users", label: "Users", permission: "UsersView", icon: Users, component: UsersTab },
  { key: "auth-providers", label: "Auth Providers", permission: "AuthProvidersView", icon: ShieldCheck, component: AuthProvidersTab },
  { key: "security", label: "Security", permission: "SettingsView", icon: Lock, component: SecurityTab },
  { key: "email", label: "Email", permission: "SettingsView", icon: Mail, component: EmailTab },
];

export default function AdminPage() {
  const { can } = usePermissions();

  const visibleTabs = adminTabs.filter((tab) => can(tab.permission));

  if (visibleTabs.length === 0) {
    return (
      <div className="text-center text-muted-foreground py-12">
        You do not have permission to view this page.
      </div>
    );
  }

  const defaultTab = visibleTabs[0].key;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Administration</h1>
        <p className="text-sm text-muted-foreground">Manage users, authentication, and security settings.</p>
      </div>

      <Tabs defaultValue={defaultTab}>
        <TabsList>
          {visibleTabs.map((tab) => (
            <TabsTrigger key={tab.key} value={tab.key}>
              <tab.icon className="mr-1.5 h-3.5 w-3.5" />
              {tab.label}
            </TabsTrigger>
          ))}
        </TabsList>
        {visibleTabs.map((tab) => (
          <TabsContent key={tab.key} value={tab.key} className="mt-6">
            <tab.component />
          </TabsContent>
        ))}
      </Tabs>
    </div>
  );
}
