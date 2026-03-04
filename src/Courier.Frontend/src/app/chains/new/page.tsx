"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useCreateChain } from "@/lib/hooks/use-chain-mutations";
import { toast } from "sonner";

export default function NewChainPage() {
  const router = useRouter();
  const createChain = useCreateChain();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    createChain.mutate(
      { name, description: description || undefined },
      {
        onSuccess: (response) => {
          toast.success("Chain created");
          router.push(`/chains/${response.data!.id}`);
        },
        onError: (error) => {
          toast.error(error.message);
        },
      }
    );
  }

  return (
    <div className="max-w-xl">
      <h1 className="text-2xl font-bold tracking-tight mb-6">Create Chain</h1>

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
                placeholder="e.g., Daily SFTP Pipeline"
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
                placeholder="What does this chain do?"
                className="mt-1"
                rows={3}
              />
            </div>
            <div className="flex gap-2 pt-2">
              <Button type="submit" disabled={!name || createChain.isPending}>
                {createChain.isPending ? "Creating..." : "Create"}
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
