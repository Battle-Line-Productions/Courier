"use client";

import { ThumbsUp, ExternalLink } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { FeedbackItemDto } from "@/lib/types";
import { useVoteFeedback, useUnvoteFeedback } from "@/lib/hooks/use-feedback-mutations";
import { cn } from "@/lib/utils";

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days}d ago`;
  const months = Math.floor(days / 30);
  return `${months}mo ago`;
}

interface FeedbackCardProps {
  item: FeedbackItemDto;
  isGitHubLinked: boolean;
}

export function FeedbackCard({ item, isGitHubLinked }: FeedbackCardProps) {
  const voteMutation = useVoteFeedback();
  const unvoteMutation = useUnvoteFeedback();

  const handleVote = () => {
    if (!isGitHubLinked) return;
    if (item.hasVoted) {
      unvoteMutation.mutate(item.number);
    } else {
      voteMutation.mutate(item.number);
    }
  };

  const isVoting = voteMutation.isPending || unvoteMutation.isPending;

  return (
    <Card className="transition-colors hover:border-primary/20">
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <CardTitle className="text-sm font-medium leading-snug">
              {item.title}
            </CardTitle>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <Badge variant={item.state === "open" ? "default" : "secondary"}>
              {item.state}
            </Badge>
            <a
              href={item.url}
              target="_blank"
              rel="noopener noreferrer"
              className="text-muted-foreground hover:text-foreground transition-colors"
              title="View on GitHub"
            >
              <ExternalLink className="h-3.5 w-3.5" />
            </a>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {item.body && (
          <p className="text-sm text-muted-foreground line-clamp-2 mb-3">
            {item.body}
          </p>
        )}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3 text-xs text-muted-foreground">
            <span>{item.authorLogin}</span>
            <span>{timeAgo(item.createdAt)}</span>
            {item.labels.filter(l => l !== "bug" && l !== "enhancement").map((label) => (
              <Badge key={label} variant="outline" className="text-[10px] px-1.5 py-0">
                {label}
              </Badge>
            ))}
          </div>
          <Button
            variant="ghost"
            size="sm"
            className={cn(
              "gap-1.5 h-8 px-2",
              item.hasVoted && "text-primary"
            )}
            onClick={handleVote}
            disabled={!isGitHubLinked || isVoting}
            title={!isGitHubLinked ? "Connect GitHub to vote" : item.hasVoted ? "Remove vote" : "Vote"}
          >
            <ThumbsUp className={cn("h-3.5 w-3.5", item.hasVoted && "fill-current")} />
            <span className="tabular-nums text-xs">{item.voteCount}</span>
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
