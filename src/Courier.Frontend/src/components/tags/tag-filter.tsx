"use client";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { TagBadge } from "./tag-badge";
import { useAllTags } from "@/lib/hooks/use-tags";

interface TagFilterProps {
  value: string;
  onChange: (value: string) => void;
}

export function TagFilter({ value, onChange }: TagFilterProps) {
  const { data } = useAllTags();
  const allTags = data?.data ?? [];

  return (
    <Select
      value={value}
      onValueChange={(v) => onChange(v === "all" ? "" : v)}
    >
      <SelectTrigger className="w-40">
        <SelectValue placeholder="Tag" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="all">All Tags</SelectItem>
        {allTags.map((tag) => (
          <SelectItem key={tag.id} value={tag.name}>
            <TagBadge name={tag.name} color={tag.color} />
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
