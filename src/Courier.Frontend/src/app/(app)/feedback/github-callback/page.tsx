"use client";

import { useEffect, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { CheckCircle2, XCircle, Loader2 } from "lucide-react";

function GitHubCallbackContent() {
  const searchParams = useSearchParams();
  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");

  useEffect(() => {
    const code = searchParams.get("code");
    if (code) {
      // Send the code back to the parent window
      if (window.opener) {
        window.opener.postMessage({ type: "github-oauth-callback", code }, window.location.origin);
        setStatus("success");
        setTimeout(() => window.close(), 1500);
      } else {
        // If not a popup, redirect back to feedback page
        setStatus("success");
        setTimeout(() => {
          window.location.href = "/feedback";
        }, 1500);
      }
    } else {
      setStatus("error");
    }
  }, [searchParams]);

  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <div className="text-center space-y-3">
        {status === "loading" && (
          <>
            <Loader2 className="h-8 w-8 animate-spin mx-auto text-muted-foreground" />
            <p className="text-sm text-muted-foreground">Linking GitHub account...</p>
          </>
        )}
        {status === "success" && (
          <>
            <CheckCircle2 className="h-8 w-8 mx-auto text-green-500" />
            <p className="text-sm text-muted-foreground">GitHub account linked! This window will close shortly.</p>
          </>
        )}
        {status === "error" && (
          <>
            <XCircle className="h-8 w-8 mx-auto text-destructive" />
            <p className="text-sm text-muted-foreground">Failed to link GitHub account. Please try again.</p>
          </>
        )}
      </div>
    </div>
  );
}

export default function GitHubCallbackPage() {
  return (
    <Suspense fallback={
      <div className="flex min-h-[60vh] items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    }>
      <GitHubCallbackContent />
    </Suspense>
  );
}
