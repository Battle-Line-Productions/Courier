"use client";

import Image from "next/image";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent } from "@/components/ui/dialog";
import {
  screenshots,
  categories,
  type ScreenshotCategory,
} from "@/lib/screenshots";
import { cn } from "@/lib/utils";

export default function ScreenshotsPage() {
  const [activeCategory, setActiveCategory] = useState<
    ScreenshotCategory | "All"
  >("All");
  const [lightboxImage, setLightboxImage] = useState<string | null>(null);

  const filtered =
    activeCategory === "All"
      ? screenshots
      : screenshots.filter((s) => s.category === activeCategory);

  return (
    <div className="mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-2xl text-center">
        <h1 className="text-3xl font-bold tracking-tight">Screenshots</h1>
        <p className="mt-4 text-lg text-muted-foreground">
          See Courier MFT in action — explore every feature of the platform.
        </p>
      </div>

      {/* Category filter */}
      <div className="mt-8 flex flex-wrap justify-center gap-2">
        <Button
          variant={activeCategory === "All" ? "default" : "outline"}
          size="sm"
          onClick={() => setActiveCategory("All")}
        >
          All
        </Button>
        {categories.map((cat) => (
          <Button
            key={cat}
            variant={activeCategory === cat ? "default" : "outline"}
            size="sm"
            onClick={() => setActiveCategory(cat)}
          >
            {cat}
          </Button>
        ))}
      </div>

      {/* Gallery grid */}
      <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {filtered.map((img) => (
          <button
            key={img.src}
            type="button"
            className="group cursor-pointer overflow-hidden rounded-lg border bg-card text-left transition-colors hover:border-primary/50"
            onClick={() => setLightboxImage(img.src)}
          >
            <div className="relative aspect-video overflow-hidden">
              <Image
                src={img.src}
                alt={img.alt}
                fill
                className="object-cover object-top transition-transform group-hover:scale-105"
                sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
              />
            </div>
            <div className="p-3">
              <p className="text-sm font-medium">{img.caption}</p>
              <p className="text-xs text-muted-foreground">{img.category}</p>
            </div>
          </button>
        ))}
      </div>

      {/* Lightbox */}
      <Dialog
        open={lightboxImage !== null}
        onOpenChange={() => setLightboxImage(null)}
      >
        <DialogContent className="max-w-5xl p-0">
          {lightboxImage && (
            <div className="relative aspect-video w-full">
              <Image
                src={lightboxImage}
                alt="Screenshot preview"
                fill
                className="object-contain"
                sizes="90vw"
              />
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
