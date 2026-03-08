"use client";

import { useState, useMemo } from "react";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Search } from "lucide-react";
import { STEP_TYPE_GROUPS, getCategoryMeta } from "./step-constants";

interface StepTypePickerProps {
  value: string;
  onChange: (typeKey: string) => void;
  trigger: React.ReactNode;
}

export function StepTypePicker({ value, onChange, trigger }: StepTypePickerProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [activeTab, setActiveTab] = useState(STEP_TYPE_GROUPS[0].category);

  const filteredGroups = useMemo(() => {
    if (!search.trim()) return STEP_TYPE_GROUPS;
    const q = search.toLowerCase();
    return STEP_TYPE_GROUPS.map((group) => ({
      ...group,
      types: group.types.filter(
        (t) =>
          t.label.toLowerCase().includes(q) ||
          t.value.toLowerCase().includes(q) ||
          t.description.toLowerCase().includes(q)
      ),
    })).filter((g) => g.types.length > 0);
  }, [search]);

  function handleSelect(typeKey: string) {
    onChange(typeKey);
    setOpen(false);
    setSearch("");
  }

  const showAllResults = search.trim().length > 0;

  return (
    <Popover open={open} onOpenChange={(v) => { setOpen(v); if (!v) setSearch(""); }}>
      <PopoverTrigger asChild>{trigger}</PopoverTrigger>
      <PopoverContent className="w-[520px] p-0" align="start">
        <div className="p-3 border-b">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Search step types..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-9"
            />
          </div>
        </div>

        {showAllResults ? (
          <ScrollArea className="max-h-[350px]">
            <div className="space-y-3 p-3">
              {filteredGroups.map((group) => (
                <div key={group.category}>
                  <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                    {group.label}
                  </h4>
                  <TypeGrid
                    types={group.types}
                    category={group.category}
                    selectedValue={value}
                    onSelect={handleSelect}
                  />
                </div>
              ))}
              {filteredGroups.length === 0 && (
                <p className="text-sm text-muted-foreground text-center py-6">
                  No step types match &ldquo;{search}&rdquo;
                </p>
              )}
            </div>
          </ScrollArea>
        ) : (
          <Tabs value={activeTab} onValueChange={setActiveTab}>
            <div className="px-3 pt-2">
              <TabsList className="w-full flex-wrap h-auto gap-1 bg-transparent p-0">
                {STEP_TYPE_GROUPS.map((group) => {
                  const meta = getCategoryMeta(group.types[0].value);
                  const Icon = meta.icon;
                  return (
                    <TabsTrigger
                      key={group.category}
                      value={group.category}
                      className="flex items-center gap-1.5 text-xs data-[state=active]:shadow-none"
                    >
                      <Icon className="h-3.5 w-3.5" />
                      {group.label}
                    </TabsTrigger>
                  );
                })}
              </TabsList>
            </div>
            <ScrollArea className="max-h-[300px]">
              {STEP_TYPE_GROUPS.map((group) => (
                <TabsContent key={group.category} value={group.category} className="mt-0 p-3">
                  <TypeGrid
                    types={group.types}
                    category={group.category}
                    selectedValue={value}
                    onSelect={handleSelect}
                  />
                </TabsContent>
              ))}
            </ScrollArea>
          </Tabs>
        )}
      </PopoverContent>
    </Popover>
  );
}

interface TypeGridProps {
  types: { value: string; label: string; description: string }[];
  category: string;
  selectedValue: string;
  onSelect: (typeKey: string) => void;
}

function TypeGrid({ types, category, selectedValue, onSelect }: TypeGridProps) {
  const meta = getCategoryMeta(types[0]?.value ?? `${category}.x`);
  const Icon = meta.icon;

  return (
    <div className="grid grid-cols-2 gap-2">
      {types.map((t) => (
        <button
          key={t.value}
          type="button"
          onClick={() => onSelect(t.value)}
          className={`
            flex items-start gap-3 rounded-lg border p-3 text-left transition-colors
            hover:bg-accent hover:border-accent-foreground/20
            ${selectedValue === t.value ? "border-primary bg-accent" : ""}
          `}
        >
          <div className={`mt-0.5 rounded-md p-1.5 ${meta.color} text-white shrink-0`}>
            <Icon className="h-3.5 w-3.5" />
          </div>
          <div className="min-w-0">
            <div className="text-sm font-medium leading-tight">{t.label}</div>
            <div className="text-xs text-muted-foreground mt-0.5 leading-tight">{t.description}</div>
          </div>
        </button>
      ))}
    </div>
  );
}
