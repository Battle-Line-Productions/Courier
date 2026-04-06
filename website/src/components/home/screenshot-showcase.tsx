import Image from "next/image";
import Link from "next/link";
import { Button } from "@/components/ui/button";

const showcaseImages = [
  {
    src: "/screenshots/dashboard.png",
    alt: "Courier MFT dashboard showing job execution overview, recent activity, and system health",
    caption: "Dashboard",
  },
  {
    src: "/screenshots/job-steps.png",
    alt: "Job pipeline builder showing multi-step file transfer configuration with SFTP, PGP, and file operations",
    caption: "Job Pipeline Builder",
  },
  {
    src: "/screenshots/connections-list.png",
    alt: "Connection management interface for SFTP, FTP, and local filesystem connections",
    caption: "Connection Management",
  },
  {
    src: "/screenshots/audit-log.png",
    alt: "Audit log showing timestamped records of all user actions and system events",
    caption: "Audit Trail",
  },
];

export function ScreenshotShowcase() {
  return (
    <section className="border-t bg-muted/30 py-20">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="text-3xl font-bold tracking-tight">
            See It in Action
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            A modern, intuitive interface for managing all your file transfer
            operations.
          </p>
        </div>
        <div className="mt-12 grid gap-6 sm:grid-cols-2">
          {showcaseImages.map((img) => (
            <div
              key={img.src}
              className="group overflow-hidden rounded-lg border bg-card"
            >
              <div className="relative aspect-video overflow-hidden">
                <Image
                  src={img.src}
                  alt={img.alt}
                  fill
                  className="object-cover object-top transition-transform group-hover:scale-105"
                  sizes="(max-width: 768px) 100vw, 50vw"
                />
              </div>
              <div className="p-4">
                <p className="text-sm font-medium">{img.caption}</p>
              </div>
            </div>
          ))}
        </div>
        <div className="mt-8 text-center">
          <Button variant="outline" asChild>
            <Link href="/screenshots">View All Screenshots</Link>
          </Button>
        </div>
      </div>
    </section>
  );
}
