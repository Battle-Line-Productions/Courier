"use client";

import Link from "next/link";
import { ChevronLeft } from "lucide-react";
import { Button } from "@/components/ui/button";
import { NotificationRuleForm } from "@/components/notifications/notification-rule-form";

export default function NewNotificationRulePage() {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/notifications">
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Create Notification Rule</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Set up alerts for job events via webhook or email
          </p>
        </div>
      </div>

      <NotificationRuleForm />
    </div>
  );
}
