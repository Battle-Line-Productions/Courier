"use client";

import { useState } from "react";
import { X, ChevronsUpDown, Check, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { TagBadge } from "./tag-badge";
import { useAllTags } from "@/lib/hooks/use-tags";
import { useAssignTags, useUnassignTags, useCreateTag } from "@/lib/hooks/use-tag-mutations";
import { toast } from "sonner";
import type { TagSummaryDto } from "@/lib/types";

interface TagPickerProps {
  entityType: string;
  entityId: string;
  currentTags?: TagSummaryDto[];
}

export function TagPicker({ entityType, entityId, currentTags = [] }: TagPickerProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const { data } = useAllTags();
  const assignTags = useAssignTags();
  const unassignTags = useUnassignTags();
  const createTag = useCreateTag();

  const allTags = data?.data ?? [];
  const currentTagNames = currentTags.map((t) => t.name);

  const trimmedSearch = search.trim();
  const filteredTags = allTags.filter(
    (tag) => tag.name.toLowerCase().includes(trimmedSearch.toLowerCase())
  );
  const exactMatch = allTags.some(
    (tag) => tag.name.toLowerCase() === trimmedSearch.toLowerCase()
  );
  const showCreateOption = trimmedSearch.length > 0 && !exactMatch;

  function handleSelect(tagId: string, tagName: string) {
    const isAssigned = currentTagNames.includes(tagName);
    if (isAssigned) {
      unassignTags.mutate(
        {
          assignments: [{ tagId, entityType, entityId }],
        },
        {
          onSuccess: () => toast.success(`Tag "${tagName}" removed`),
          onError: (error) => toast.error(error.message),
        }
      );
    } else {
      assignTags.mutate(
        {
          assignments: [{ tagId, entityType, entityId }],
        },
        {
          onSuccess: () => toast.success(`Tag "${tagName}" added`),
          onError: (error) => toast.error(error.message),
        }
      );
    }
  }

  function handleCreateAndAssign() {
    if (!trimmedSearch) return;
    createTag.mutate(
      { name: trimmedSearch },
      {
        onSuccess: (response) => {
          const newTag = response.data;
          if (!newTag) return;
          assignTags.mutate(
            {
              assignments: [{ tagId: newTag.id, entityType, entityId }],
            },
            {
              onSuccess: () => {
                toast.success(`Tag "${trimmedSearch}" created and added`);
                setSearch("");
              },
              onError: (error) => toast.error(error.message),
            }
          );
        },
        onError: (error) => toast.error(error.message),
      }
    );
  }

  function handleRemove(tagName: string) {
    const tag = allTags.find((t) => t.name === tagName);
    if (!tag) return;
    unassignTags.mutate(
      {
        assignments: [{ tagId: tag.id, entityType, entityId }],
      },
      {
        onSuccess: () => toast.success(`Tag "${tagName}" removed`),
        onError: (error) => toast.error(error.message),
      }
    );
  }

  return (
    <div className="space-y-2">
      {currentTags.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {currentTags.map((tag) => (
            <span key={tag.name} className="inline-flex items-center gap-0.5">
              <TagBadge name={tag.name} color={tag.color} />
              <button
                type="button"
                onClick={() => handleRemove(tag.name)}
                className="ml-0.5 rounded-full p-0.5 hover:bg-muted transition-colors"
              >
                <X className="h-3 w-3 text-muted-foreground" />
              </button>
            </span>
          ))}
        </div>
      )}
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button variant="outline" size="sm" className="h-8 text-xs">
            <ChevronsUpDown className="mr-1.5 h-3.5 w-3.5" />
            Manage Tags
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-64 p-2" align="start">
          <Input
            placeholder="Search or create tags..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="h-8 text-sm mb-2"
          />
          <div className="max-h-48 overflow-y-auto space-y-0.5">
            {showCreateOption && (
              <button
                type="button"
                onClick={handleCreateAndAssign}
                disabled={createTag.isPending}
                className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent transition-colors text-primary"
              >
                <Plus className="h-3.5 w-3.5" />
                Create &quot;{trimmedSearch}&quot;
              </button>
            )}
            {filteredTags.length === 0 && !showCreateOption ? (
              <p className="text-xs text-muted-foreground py-2 text-center">
                No tags found.
              </p>
            ) : (
              filteredTags.map((tag) => {
                const isSelected = currentTagNames.includes(tag.name);
                return (
                  <button
                    key={tag.id}
                    type="button"
                    onClick={() => handleSelect(tag.id, tag.name)}
                    className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent transition-colors"
                  >
                    <span className="flex h-4 w-4 items-center justify-center">
                      {isSelected && <Check className="h-3.5 w-3.5" />}
                    </span>
                    <TagBadge name={tag.name} color={tag.color} />
                  </button>
                );
              })
            )}
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
