"use client";

import { use } from "react";
import Link from "next/link";
import { ChevronLeft } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { NotificationRuleForm } from "@/components/notifications/notification-rule-form";
import { useNotificationRule } from "@/lib/hooks/use-notification-rules";

export default function EditNotificationRulePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, isLoading } = useNotificationRule(id);

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full max-w-2xl" />
      </div>
    );
  }

  const rule = data?.data;

  if (!rule) {
    return (
      <div className="text-center py-12">
        <p className="text-muted-foreground">Notification rule not found.</p>
        <Button asChild className="mt-4">
          <Link href="/notifications">Back to Notifications</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" asChild>
          <Link href={`/notifications/${id}`}>
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Edit: {rule.name}</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Update notification rule configuration
          </p>
        </div>
      </div>

      <NotificationRuleForm rule={rule} />
    </div>
  );
}
