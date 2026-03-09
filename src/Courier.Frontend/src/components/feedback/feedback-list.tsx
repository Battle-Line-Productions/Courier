"use client";

import { FeedbackCard } from "./feedback-card";
import { EmptyState } from "@/components/shared/empty-state";
import type { FeedbackItemDto } from "@/lib/types";

interface FeedbackListProps {
  items: FeedbackItemDto[];
  isGitHubLinked: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
}

export function FeedbackList({ items, isGitHubLinked, emptyTitle, emptyDescription }: FeedbackListProps) {
  if (items.length === 0) {
    return (
      <EmptyState
        title={emptyTitle ?? "No feedback yet"}
        description={emptyDescription ?? "Be the first to submit feedback!"}
      />
    );
  }

  return (
    <div className="grid gap-3">
      {items.map((item) => (
        <FeedbackCard key={item.number} item={item} isGitHubLinked={isGitHubLinked} />
      ))}
    </div>
  );
}
