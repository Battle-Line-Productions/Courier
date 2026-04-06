"use client";

import { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/navigation";
import { Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { searchDocs, type SearchResult } from "@/lib/search";
import type { DocPage } from "@/lib/docs";

interface SearchDialogProps {
  docs: Array<Pick<DocPage, "slug" | "title" | "description" | "content">>;
}

export function SearchDialog({ docs }: SearchDialogProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SearchResult[]>([]);
  const router = useRouter();

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key === "k") {
        e.preventDefault();
        setOpen(true);
      }
    }
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, []);

  const handleSearch = useCallback(
    (value: string) => {
      setQuery(value);
      setResults(searchDocs(docs, value));
    },
    [docs]
  );

  function handleSelect(slug: string) {
    setOpen(false);
    setQuery("");
    setResults([]);
    router.push(`/docs/${slug}`);
  }

  return (
    <>
      <Button
        variant="outline"
        size="sm"
        className="hidden gap-2 text-muted-foreground sm:flex"
        onClick={() => setOpen(true)}
      >
        <Search className="h-4 w-4" />
        <span>Search docs...</span>
        <kbd className="ml-2 rounded border bg-muted px-1.5 text-xs">
          Ctrl+K
        </kbd>
      </Button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="gap-0 p-0 sm:max-w-lg">
          <div className="flex items-center border-b px-3">
            <Search className="h-4 w-4 shrink-0 text-muted-foreground" />
            <Input
              placeholder="Search documentation..."
              className="border-0 focus-visible:ring-0"
              value={query}
              onChange={(e) => handleSearch(e.target.value)}
              autoFocus
            />
          </div>
          {results.length > 0 && (
            <div className="max-h-80 overflow-y-auto p-2">
              {results.map((result) => (
                <button
                  key={result.slug}
                  type="button"
                  className="w-full rounded-md px-3 py-2 text-left transition-colors hover:bg-muted"
                  onClick={() => handleSelect(result.slug)}
                >
                  <div className="text-sm font-medium">{result.title}</div>
                  <div className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">
                    {result.matchedContent}
                  </div>
                </button>
              ))}
            </div>
          )}
          {query && results.length === 0 && (
            <div className="p-6 text-center text-sm text-muted-foreground">
              No results found for &ldquo;{query}&rdquo;
            </div>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
