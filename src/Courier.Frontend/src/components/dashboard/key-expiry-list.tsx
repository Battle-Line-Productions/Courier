"use client";

import Link from "next/link";
import { AlertTriangle } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useExpiringKeys } from "@/lib/hooks/use-dashboard";
import { cn } from "@/lib/utils";

function expiryColor(days: number): string {
  if (days < 7) return "text-red-600";
  if (days < 14) return "text-amber-600";
  return "text-muted-foreground";
}

function expiryBg(days: number): string {
  if (days < 7) return "bg-red-50 border-red-200";
  if (days < 14) return "bg-amber-50 border-amber-200";
  return "bg-muted/50";
}

export function KeyExpiryList() {
  const { data, isLoading } = useExpiringKeys(30);
  const keys = data?.data;

  if (isLoading) {
    return (
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-medium">Key Expiry Warnings</CardTitle>
        </CardHeader>
        <CardContent>
          <Skeleton className="h-12 w-full" />
        </CardContent>
      </Card>
    );
  }

  if (!keys?.length) return null;

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-center gap-2">
          <AlertTriangle className="h-4 w-4 text-amber-500" />
          <CardTitle className="text-sm font-medium">Key Expiry Warnings</CardTitle>
        </div>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {keys.map((key) => (
            <div
              key={key.id}
              className={cn("flex items-center justify-between rounded-lg border p-3", expiryBg(key.daysUntilExpiry))}
            >
              <div className="min-w-0 flex-1">
                <Link
                  href={`/keys?tab=pgp&id=${key.id}`}
                  className="text-sm font-medium text-blue-600 hover:underline"
                >
                  {key.name}
                </Link>
                {key.fingerprint && (
                  <p className="mt-0.5 truncate font-mono text-xs text-muted-foreground">
                    {key.fingerprint}
                  </p>
                )}
              </div>
              <div className={cn("ml-4 text-right text-sm font-semibold tabular-nums", expiryColor(key.daysUntilExpiry))}>
                {key.daysUntilExpiry <= 0 ? "Expired" : `${key.daysUntilExpiry}d`}
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
