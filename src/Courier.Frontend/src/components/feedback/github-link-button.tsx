"use client";

import { useState } from "react";
import { Github, Unlink } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useLinkGitHub, useUnlinkGitHub } from "@/lib/hooks/use-feedback-mutations";
import { api, ApiClientError } from "@/lib/api";
import { toast } from "sonner";

interface GitHubLinkButtonProps {
  isLinked: boolean;
  gitHubUsername?: string;
  onLinkChange?: () => void;
}

export function GitHubLinkButton({ isLinked, gitHubUsername, onLinkChange }: GitHubLinkButtonProps) {
  const [loading, setLoading] = useState(false);
  const linkMutation = useLinkGitHub();
  const unlinkMutation = useUnlinkGitHub();

  const handleLink = async () => {
    setLoading(true);
    try {
      const response = await api.getGitHubAuthUrl();
      const authUrl = response.data!.url;

      // Open GitHub auth in a popup
      const width = 600;
      const height = 700;
      const left = window.screenX + (window.outerWidth - width) / 2;
      const top = window.screenY + (window.outerHeight - height) / 2;
      const popup = window.open(
        authUrl,
        "github-oauth",
        `width=${width},height=${height},left=${left},top=${top}`
      );

      // Listen for the callback
      const handleMessage = async (event: MessageEvent) => {
        if (event.data?.type === "github-oauth-callback" && event.data?.code) {
          window.removeEventListener("message", handleMessage);
          try {
            await linkMutation.mutateAsync(event.data.code);
            toast.success("GitHub account linked successfully!");
            onLinkChange?.();
          } catch {
            toast.error("Failed to link GitHub account.");
          }
          setLoading(false);
        }
      };

      window.addEventListener("message", handleMessage);

      // Also poll for popup close
      const interval = setInterval(() => {
        if (popup?.closed) {
          clearInterval(interval);
          window.removeEventListener("message", handleMessage);
          setLoading(false);
        }
      }, 500);
    } catch (err) {
      const message = err instanceof ApiClientError
        ? err.message
        : "Failed to get GitHub authorization URL.";
      toast.error(message);
      setLoading(false);
    }
  };

  const handleUnlink = async () => {
    try {
      await unlinkMutation.mutateAsync();
      toast.success("GitHub account unlinked.");
      onLinkChange?.();
    } catch {
      toast.error("Failed to unlink GitHub account.");
    }
  };

  if (isLinked) {
    return (
      <div className="flex items-center gap-2">
        <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
          <Github className="h-4 w-4" />
          <span>{gitHubUsername}</span>
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={handleUnlink}
          disabled={unlinkMutation.isPending}
          className="text-muted-foreground hover:text-destructive"
        >
          <Unlink className="h-3.5 w-3.5" />
        </Button>
      </div>
    );
  }

  return (
    <Button variant="outline" onClick={handleLink} disabled={loading}>
      <Github className="mr-2 h-4 w-4" />
      {loading ? "Connecting..." : "Connect GitHub"}
    </Button>
  );
}
