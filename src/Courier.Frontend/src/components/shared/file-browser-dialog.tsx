"use client";

import { useState } from "react";
import { Folder, File, ChevronRight, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from "@/components/ui/dialog";
import { useFilesystemBrowse } from "@/lib/hooks/use-filesystem";
import type { FileEntry } from "@/lib/types";

interface FileBrowserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSelect: (path: string) => void;
  mode?: "file" | "directory" | "both";
}

export function FileBrowserDialog({
  open,
  onOpenChange,
  onSelect,
  mode = "both",
}: FileBrowserDialogProps) {
  const [currentPath, setCurrentPath] = useState<string | undefined>(undefined);
  const [selected, setSelected] = useState<string | null>(null);

  const { data, isLoading, isError } = useFilesystemBrowse(
    open ? currentPath : undefined
  );

  const browseResult = data?.data;

  function navigateTo(path: string) {
    setCurrentPath(path);
    setSelected(null);
  }

  function handleEntryClick(entry: FileEntry) {
    if (entry.type === "directory") {
      const sep = browseResult?.currentPath ? "/" : "";
      const fullPath = browseResult?.currentPath
        ? `${browseResult.currentPath}${sep}${entry.name}`
        : entry.name;
      navigateTo(fullPath);
    } else {
      const fullPath = `${browseResult?.currentPath}/${entry.name}`;
      setSelected(fullPath);
    }
  }

  function handleSelect() {
    const path = selected ?? browseResult?.currentPath;
    if (path) {
      onSelect(path);
      onOpenChange(false);
      setCurrentPath(undefined);
      setSelected(null);
    }
  }

  function handleOpenChange(open: boolean) {
    if (!open) {
      setCurrentPath(undefined);
      setSelected(null);
    }
    onOpenChange(open);
  }

  const breadcrumbs = buildBreadcrumbs(browseResult?.currentPath ?? "");

  const visibleEntries = browseResult?.entries.filter((entry) => {
    if (mode === "file") return true;
    if (mode === "directory") return true;
    return true;
  }) ?? [];

  const canSelect =
    mode === "directory"
      ? !!browseResult?.currentPath
      : mode === "file"
        ? !!selected
        : !!selected || !!browseResult?.currentPath;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Browse Filesystem</DialogTitle>
          <DialogDescription>
            Navigate and select a {mode === "file" ? "file" : mode === "directory" ? "folder" : "path"}.
          </DialogDescription>
        </DialogHeader>

        {/* Breadcrumb bar */}
        <nav className="flex items-center gap-1 text-sm overflow-x-auto pb-1">
          <button
            onClick={() => navigateTo("")}
            className="text-muted-foreground hover:text-foreground shrink-0"
          >
            Root
          </button>
          {breadcrumbs.map((crumb) => (
            <span key={crumb.path} className="flex items-center gap-1">
              <ChevronRight className="size-3 text-muted-foreground shrink-0" />
              <button
                onClick={() => navigateTo(crumb.path)}
                className="text-muted-foreground hover:text-foreground truncate max-w-32"
              >
                {crumb.name}
              </button>
            </span>
          ))}
        </nav>

        {/* Entry list */}
        <div className="border rounded-md h-64 overflow-y-auto">
          {isLoading ? (
            <div className="flex items-center justify-center h-full">
              <Loader2 className="size-5 animate-spin text-muted-foreground" />
            </div>
          ) : isError ? (
            <div className="flex items-center justify-center h-full text-sm text-destructive">
              Failed to load directory.
            </div>
          ) : visibleEntries.length === 0 ? (
            <div className="flex items-center justify-center h-full text-sm text-muted-foreground">
              Empty directory
            </div>
          ) : (
            <ul className="divide-y">
              {browseResult?.parentPath !== undefined && browseResult.parentPath !== null && (
                <li>
                  <button
                    onClick={() => navigateTo(browseResult.parentPath!)}
                    className="flex items-center gap-2 w-full px-3 py-2 text-sm hover:bg-accent text-left"
                  >
                    <Folder className="size-4 text-muted-foreground" />
                    <span className="text-muted-foreground">..</span>
                  </button>
                </li>
              )}
              {visibleEntries.map((entry) => {
                const fullPath =
                  entry.type === "file"
                    ? `${browseResult?.currentPath}/${entry.name}`
                    : null;
                const isSelected = fullPath !== null && selected === fullPath;

                return (
                  <li key={entry.name}>
                    <button
                      onClick={() => handleEntryClick(entry)}
                      className={`flex items-center gap-2 w-full px-3 py-2 text-sm hover:bg-accent text-left ${
                        isSelected ? "bg-accent" : ""
                      }`}
                    >
                      {entry.type === "directory" ? (
                        <Folder className="size-4 text-blue-500 shrink-0" />
                      ) : (
                        <File className="size-4 text-muted-foreground shrink-0" />
                      )}
                      <span className="truncate">{entry.name}</span>
                      {entry.size != null && entry.type === "file" && (
                        <span className="ml-auto text-xs text-muted-foreground shrink-0">
                          {formatFileSize(entry.size)}
                        </span>
                      )}
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>

        {/* Selected path display */}
        {(selected || (mode !== "file" && browseResult?.currentPath)) && (
          <p className="text-xs text-muted-foreground truncate">
            Selected: {selected ?? browseResult?.currentPath}
          </p>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSelect} disabled={!canSelect}>
            Select
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function buildBreadcrumbs(path: string) {
  if (!path) return [];

  // Handle Windows paths (C:\foo\bar) and Unix paths (/foo/bar)
  const normalized = path.replace(/\\/g, "/");
  const parts = normalized.split("/").filter(Boolean);
  const crumbs: { name: string; path: string }[] = [];

  for (let i = 0; i < parts.length; i++) {
    const crumbPath = parts.slice(0, i + 1).join("/");
    // Restore drive letter format for Windows (e.g., C:/)
    const resolvedPath = /^[a-zA-Z]:$/.test(parts[0]) && i === 0
      ? `${parts[0]}/`
      : crumbPath;
    crumbs.push({ name: parts[i], path: resolvedPath });
  }

  return crumbs;
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}
