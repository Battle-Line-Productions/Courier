"use client";

import { useState, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { FeedbackList } from "@/components/feedback/feedback-list";
import { FeedbackSubmitDialog } from "@/components/feedback/feedback-submit-dialog";
import { GitHubLinkButton } from "@/components/feedback/github-link-button";
import { useFeedbackList } from "@/lib/hooks/use-feedback";
import { useAuth } from "@/lib/auth";
import { api } from "@/lib/api";

export default function FeedbackPage() {
  const { user } = useAuth();
  const [tab, setTab] = useState("feature");
  const [state, setState] = useState("open");

  // Get full user details for GitHub link status
  const { data: userData, refetch: refetchUser } = useQuery({
    queryKey: ["users", user?.id],
    queryFn: () => api.getUser(user!.id),
    enabled: !!user?.id,
  });

  const isGitHubLinked = userData?.data?.isGitHubLinked ?? false;
  const gitHubUsername = userData?.data?.gitHubUsername;

  // Feature requests
  const [featurePage, setFeaturePage] = useState(1);
  const { data: featureData, isLoading: featureLoading } = useFeedbackList("feature", featurePage, 20, state);
  const featureItems = featureData?.data ?? [];

  // Bug reports
  const [bugPage, setBugPage] = useState(1);
  const { data: bugData, isLoading: bugLoading } = useFeedbackList("bug", bugPage, 20, state);
  const bugItems = bugData?.data ?? [];

  // Reset pagination when state filter changes
  useEffect(() => {
    setFeaturePage(1);
    setBugPage(1);
  }, [state]);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Feedback</h1>
          <p className="text-sm text-muted-foreground mt-0.5">
            Browse, vote on, and submit feature requests and bug reports
          </p>
        </div>
        <div className="flex items-center gap-3">
          <GitHubLinkButton
            isLinked={isGitHubLinked}
            gitHubUsername={gitHubUsername}
            onLinkChange={() => refetchUser()}
          />
          <FeedbackSubmitDialog isGitHubLinked={isGitHubLinked} defaultType={tab} />
        </div>
      </div>

      <Tabs value={tab} onValueChange={(v) => { setTab(v); }}>
        <div className="flex items-center justify-between">
          <TabsList>
            <TabsTrigger value="feature">Feature Requests</TabsTrigger>
            <TabsTrigger value="bug">Bug Reports</TabsTrigger>
          </TabsList>
          <Select value={state} onValueChange={setState}>
            <SelectTrigger className="w-28">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="open">Open</SelectItem>
              <SelectItem value="closed">Closed</SelectItem>
              <SelectItem value="all">All</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <TabsContent value="feature" className="space-y-4 mt-4">
          {featureLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-24 w-full" />
              <Skeleton className="h-24 w-full" />
              <Skeleton className="h-24 w-full" />
            </div>
          ) : (
            <>
              <FeedbackList
                items={featureItems}
                isGitHubLinked={isGitHubLinked}
                emptyTitle="No feature requests yet"
                emptyDescription="Be the first to submit a feature request!"
              />
              {featureItems.length > 0 && (
                <div className="flex items-center justify-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={featurePage <= 1}
                    onClick={() => setFeaturePage((p) => p - 1)}
                  >
                    Previous
                  </Button>
                  <span className="text-sm text-muted-foreground tabular-nums">
                    Page {featurePage}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={featureItems.length < 20}
                    onClick={() => setFeaturePage((p) => p + 1)}
                  >
                    Next
                  </Button>
                </div>
              )}
            </>
          )}
        </TabsContent>

        <TabsContent value="bug" className="space-y-4 mt-4">
          {bugLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-24 w-full" />
              <Skeleton className="h-24 w-full" />
              <Skeleton className="h-24 w-full" />
            </div>
          ) : (
            <>
              <FeedbackList
                items={bugItems}
                isGitHubLinked={isGitHubLinked}
                emptyTitle="No bug reports yet"
                emptyDescription="No bugs reported — that's great!"
              />
              {bugItems.length > 0 && (
                <div className="flex items-center justify-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={bugPage <= 1}
                    onClick={() => setBugPage((p) => p - 1)}
                  >
                    Previous
                  </Button>
                  <span className="text-sm text-muted-foreground tabular-nums">
                    Page {bugPage}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={bugItems.length < 20}
                    onClick={() => setBugPage((p) => p + 1)}
                  >
                    Next
                  </Button>
                </div>
              )}
            </>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
