"use client";

import { use, useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useChain } from "@/lib/hooks/use-chains";
import { useUpdateChain } from "@/lib/hooks/use-chain-mutations";
import { toast } from "sonner";
import { usePermissions } from "@/lib/hooks/use-permissions";

export default function EditChainPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const { can } = usePermissions();
  const { data, isLoading } = useChain(id);
  const updateChain = useUpdateChain(id);

  if (!can("ChainsEdit")) {
    router.push(`/chains/${id}`);
    return null;
  }
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  useEffect(() => {
    if (data?.data) {
      setName(data.data.name);
      setDescription(data.data.description || "");
    }
  }, [data]);

  if (isLoading) {
    return (
      <div className="space-y-4 max-w-xl">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (!data?.data) {
    return <p className="text-muted-foreground">Chain not found.</p>;
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    updateChain.mutate(
      { name, description: description || undefined },
      {
        onSuccess: () => {
          toast.success("Chain updated");
          router.push(`/chains/${id}`);
        },
        onError: (error) => {
          toast.error(error.message);
        },
      }
    );
  }

  return (
    <div className="max-w-xl">
      <h1 className="text-2xl font-bold tracking-tight mb-6">Edit Chain</h1>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Chain Details</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <Label htmlFor="name">Name</Label>
              <Input
                id="name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="description">Description</Label>
              <Textarea
                id="description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="mt-1"
                rows={3}
              />
            </div>
            <div className="flex gap-2 pt-2">
              <Button type="submit" disabled={!name || updateChain.isPending}>
                {updateChain.isPending ? "Saving..." : "Save"}
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => router.back()}
              >
                Cancel
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
