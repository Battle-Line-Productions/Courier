"use client";

interface GuideImageProps {
  src: string;
  alt: string;
  caption?: string;
}

export function GuideImage({ src, alt, caption }: GuideImageProps) {
  return (
    <figure className="my-6">
      <div className="overflow-hidden rounded-lg border shadow-sm">
        <img
          src={src}
          alt={alt}
          className="w-full"
          loading="lazy"
        />
      </div>
      {caption && (
        <figcaption className="mt-2 text-center text-sm text-muted-foreground">
          {caption}
        </figcaption>
      )}
    </figure>
  );
}
