import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ArrowRight, GitFork } from "lucide-react";

export function Hero() {
  return (
    <section className="relative overflow-hidden py-20 sm:py-32">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-3xl text-center">
          <p className="text-sm font-medium uppercase tracking-widest text-primary">
            Open Source
          </p>
          <h1 className="mt-4 text-4xl font-bold tracking-tight sm:text-6xl">
            Enterprise File Transfer,{" "}
            <span className="text-primary">Simplified</span>
          </h1>
          <p className="mt-6 text-lg leading-8 text-muted-foreground">
            Replace SFTP scripts, PGP workflows, and cron jobs with a single
            auditable platform. Build multi-step file transfer pipelines with
            encryption, scheduling, and monitoring — all self-hosted.
          </p>
          <div className="mt-10 flex items-center justify-center gap-4">
            <Button size="lg" asChild>
              <Link href="/docs/getting-started">
                Get Started <ArrowRight className="ml-2 h-4 w-4" />
              </Link>
            </Button>
            <Button variant="outline" size="lg" asChild>
              <a
                href="https://github.com/Battle-Line-Productions/Courier"
                target="_blank"
                rel="noopener noreferrer"
              >
                <GitFork className="mr-2 h-4 w-4" />
                GitHub
              </a>
            </Button>
          </div>
        </div>
      </div>
    </section>
  );
}
