"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { api, ApiClientError } from "@/lib/api";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";

function AuthCallbackInner() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { loginWithTokens } = useAuth();

  const [error, setError] = useState<string | null>(null);
  const [isProcessing, setIsProcessing] = useState(true);

  useEffect(() => {
    const errorParam = searchParams.get("error");
    const code = searchParams.get("code");

    if (errorParam) {
      setError(decodeURIComponent(errorParam));
      setIsProcessing(false);
      return;
    }

    if (!code) {
      setError("No authorization code received.");
      setIsProcessing(false);
      return;
    }

    async function exchange() {
      try {
        const response = await api.exchangeSsoCode(code!);
        if (response.data) {
          loginWithTokens(
            response.data.accessToken,
            response.data.refreshToken,
            response.data.user,
            response.data.expiresIn
          );
          router.push("/");
        } else {
          setError(response.error?.message ?? "SSO exchange failed.");
          setIsProcessing(false);
        }
      } catch (err) {
        if (err instanceof ApiClientError) {
          setError(err.message);
        } else {
          setError("An unexpected error occurred during sign-in.");
        }
        setIsProcessing(false);
      }
    }

    exchange();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  if (isProcessing) {
    return (
      <div className="flex flex-col items-center gap-4 py-6">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
        <p className="text-sm text-muted-foreground">Completing sign-in...</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center gap-4 py-6 text-center">
      <div className="rounded-full bg-destructive/10 p-3">
        <svg
          className="h-6 w-6 text-destructive"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={2}
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </div>
      <div>
        <h2 className="text-lg font-semibold">Sign-in Failed</h2>
        <p className="mt-1 text-sm text-muted-foreground">{error}</p>
      </div>
      <Button variant="outline" onClick={() => router.push("/login")}>
        Back to Login
      </Button>
    </div>
  );
}

export default function AuthCallbackPage() {
  return (
    <div className="rounded-xl border bg-card p-8 shadow-sm">
      <Suspense
        fallback={
          <div className="flex flex-col items-center gap-4 py-6">
            <Loader2 className="h-8 w-8 animate-spin text-primary" />
            <p className="text-sm text-muted-foreground">Loading...</p>
          </div>
        }
      >
        <AuthCallbackInner />
      </Suspense>
    </div>
  );
}
