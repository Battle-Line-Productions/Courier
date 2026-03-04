import { Badge } from "@/components/ui/badge";

interface TagBadgeProps {
  name: string;
  color?: string;
}

export function TagBadge({ name, color }: TagBadgeProps) {
  return (
    <Badge
      variant="secondary"
      style={
        color
          ? { backgroundColor: color + "20", color: color, borderColor: color + "40" }
          : undefined
      }
      className="text-xs"
    >
      {name}
    </Badge>
  );
}
