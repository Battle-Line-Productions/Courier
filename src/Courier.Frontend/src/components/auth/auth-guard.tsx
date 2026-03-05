"use client";

import { useAuth } from "@/lib/auth";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Loader2 } from "lucide-react";

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const [setupChecked, setSetupChecked] = useState(false);

  useEffect(() => {
    async function checkSetup() {
      try {
        const response = await api.getSetupStatus();
        if (response.data && !response.data.isCompleted) {
          router.push("/setup");
          return;
        }
      } catch {
        // If we can't check setup status, proceed normally
      }
      setSetupChecked(true);
    }
    checkSetup();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  if (isLoading || !setupChecked) {
    return (
      <div className="flex h-screen items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!isAuthenticated) {
    return null; // AuthProvider handles redirect
  }

  return <>{children}</>;
}
