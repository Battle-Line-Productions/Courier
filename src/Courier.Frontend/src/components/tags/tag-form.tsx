"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useCreateTag, useUpdateTag } from "@/lib/hooks/use-tag-mutations";
import { toast } from "sonner";
import type { TagDto } from "@/lib/types";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";

interface TagFormProps {
  tag?: TagDto;
}

export function TagForm({ tag }: TagFormProps) {
  const router = useRouter();
  const isEdit = !!tag;

  const [name, setName] = useState(tag?.name ?? "");
  const [color, setColor] = useState(tag?.color ?? "");
  const [category, setCategory] = useState(tag?.category ?? "");
  const [description, setDescription] = useState(tag?.description ?? "");
  const [nameError, setNameError] = useState("");

  const createTag = useCreateTag();
  const updateTag = useUpdateTag(tag?.id ?? "");
  const isSubmitting = createTag.isPending || updateTag.isPending;

  function validate(): boolean {
    if (!name.trim()) {
      setNameError("Name is required");
      return false;
    }
    if (name.length > 50) {
      setNameError("Name must be 50 characters or less");
      return false;
    }
    setNameError("");
    return true;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!validate()) return;

    const data = {
      name: name.trim(),
      color: color.trim() || undefined,
      category: category.trim() || undefined,
      description: description.trim() || undefined,
    };

    try {
      if (isEdit) {
        await updateTag.mutateAsync(data);
        toast.success("Tag updated");
        router.push("/tags");
      } else {
        await createTag.mutateAsync(data);
        toast.success("Tag created");
        router.push("/tags");
      }
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : "Something went wrong";
      toast.error(message);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" asChild>
          <Link href={isEdit ? `/tags/${tag.id}` : "/tags"}>
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </Button>
        <h1 className="text-2xl font-bold tracking-tight">
          {isEdit ? "Edit Tag" : "Create Tag"}
        </h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Tag Details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-1.5">
            <Label htmlFor="name">Name</Label>
            <Input
              id="name"
              placeholder="e.g., production"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
            {nameError && (
              <p className="text-sm text-destructive">{nameError}</p>
            )}
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="color">Color</Label>
            <div className="flex items-center gap-3">
              <Input
                id="color"
                placeholder="#3b82f6"
                value={color}
                onChange={(e) => setColor(e.target.value)}
                className="max-w-48"
              />
              {color && (
                <div
                  className="h-8 w-8 rounded-md border"
                  style={{ backgroundColor: color }}
                />
              )}
            </div>
            <p className="text-xs text-muted-foreground">
              Hex color code for the tag badge
            </p>
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="category">Category</Label>
            <Input
              id="category"
              placeholder="e.g., environment"
              value={category}
              onChange={(e) => setCategory(e.target.value)}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              placeholder="What is this tag used for?"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
            />
          </div>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-3">
        <Button variant="outline" type="button" asChild>
          <Link href={isEdit ? `/tags/${tag.id}` : "/tags"}>Cancel</Link>
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Saving..." : isEdit ? "Save Changes" : "Create Tag"}
        </Button>
      </div>
    </form>
  );
}
