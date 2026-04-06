# Courier Website Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build couriermft.com — a standalone Next.js marketing site + full documentation, deployed to Cloudflare Pages as a static export.

**Architecture:** Standalone Next.js 16 app in `website/` at repo root. Docs are sourced from existing `Docs/CourierDesignDoc.md` — a build-time script splits the design doc into individual markdown pages, which are rendered via `next-mdx-remote`. Static export to Cloudflare Pages.

**Tech Stack:** Next.js 16, React 19, TypeScript 6, Tailwind CSS 4, shadcn/ui (new-york), next-themes, next-mdx-remote, next-sitemap, gray-matter, Cloudflare Pages

**Spec:** `Docs/superpowers/specs/2026-04-05-courier-website-design.md`

---

## Phase 1: Project Foundation

### Task 1: Scaffold Next.js Project

**Files:**
- Create: `website/package.json`
- Create: `website/tsconfig.json`
- Create: `website/next.config.ts`
- Create: `website/postcss.config.mjs`
- Create: `website/src/app/layout.tsx`
- Create: `website/src/app/page.tsx`
- Create: `website/src/app/globals.css`
- Create: `website/src/lib/utils.ts`
- Modify: `.gitignore`

- [ ] **Step 1: Initialize the Next.js project**

Run from repo root:
```bash
cd website
npx create-next-app@latest . --typescript --tailwind --eslint --app --src-dir --import-alias "@/*" --use-npm
```

If it prompts about overwriting, choose yes. This creates the scaffold.

- [ ] **Step 2: Pin dependency versions to match product frontend**

Replace the dependencies in `website/package.json` with exact versions matching `src/Courier.Frontend/package.json`:

```json
{
  "name": "courier-website",
  "version": "0.1.0",
  "private": true,
  "scripts": {
    "dev": "next dev",
    "prebuild": "npx tsx scripts/sync-docs.ts && npx tsx scripts/sync-screenshots.ts",
    "build": "next build",
    "start": "next start",
    "lint": "next lint"
  },
  "dependencies": {
    "next": "^16.2.2",
    "react": "^19.1.0",
    "react-dom": "^19.1.0",
    "next-themes": "^0.4.6",
    "next-mdx-remote": "^5.0.0",
    "next-sitemap": "^4.2.3",
    "gray-matter": "^4.0.3",
    "lucide-react": "^1.7.0",
    "class-variance-authority": "^0.7.1",
    "clsx": "^2.1.1",
    "tailwind-merge": "^3.5.0",
    "remark-gfm": "^4.0.0",
    "rehype-slug": "^6.0.0",
    "rehype-highlight": "^7.0.2"
  },
  "devDependencies": {
    "typescript": "^6.0.2",
    "@types/node": "^22.0.0",
    "@types/react": "^19.1.0",
    "@types/react-dom": "^19.1.0",
    "tailwindcss": "^4.2.0",
    "@tailwindcss/postcss": "^4.2.0",
    "postcss": "^8.5.6",
    "eslint": "^9.0.0",
    "eslint-config-next": "^16.2.2",
    "tsx": "^4.19.0",
    "shadcn": "^4.1.2",
    "@tailwindcss/typography": "^0.5.16"
  }
}
```

- [ ] **Step 3: Configure next.config.ts for static export**

Write `website/next.config.ts`:

```typescript
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "export",
  images: {
    unoptimized: true,
  },
};

export default nextConfig;
```

Note: `images.unoptimized` is required for static export (no image optimization server on Cloudflare Pages).

- [ ] **Step 4: Configure PostCSS for Tailwind v4**

Write `website/postcss.config.mjs`:

```javascript
/** @type {import('postcss-load-config').Config} */
const config = {
  plugins: {
    "@tailwindcss/postcss": {},
  },
};

export default config;
```

- [ ] **Step 5: Set up globals.css with Tailwind v4**

Write `website/src/app/globals.css`:

```css
@import "tailwindcss";
@import "tailwindcss/theme" layer(theme);

@plugin "@tailwindcss/typography";

@custom-variant dark (&:is(.dark *));

:root {
  --background: oklch(1 0 0);
  --foreground: oklch(0.145 0 0);
  --card: oklch(1 0 0);
  --card-foreground: oklch(0.145 0 0);
  --popover: oklch(1 0 0);
  --popover-foreground: oklch(0.145 0 0);
  --primary: oklch(0.546 0.245 262.881);
  --primary-foreground: oklch(0.985 0.001 106.423);
  --secondary: oklch(0.97 0 0);
  --secondary-foreground: oklch(0.205 0 0);
  --muted: oklch(0.97 0 0);
  --muted-foreground: oklch(0.556 0 0);
  --accent: oklch(0.97 0 0);
  --accent-foreground: oklch(0.205 0 0);
  --destructive: oklch(0.577 0.245 27.325);
  --destructive-foreground: oklch(0.577 0.245 27.325);
  --border: oklch(0.922 0 0);
  --input: oklch(0.922 0 0);
  --ring: oklch(0.546 0.245 262.881);
  --radius: 0.625rem;
}

.dark {
  --background: oklch(0.145 0 0);
  --foreground: oklch(0.985 0 0);
  --card: oklch(0.145 0 0);
  --card-foreground: oklch(0.985 0 0);
  --popover: oklch(0.145 0 0);
  --popover-foreground: oklch(0.985 0 0);
  --primary: oklch(0.623 0.214 259.815);
  --primary-foreground: oklch(0.985 0.001 106.423);
  --secondary: oklch(0.269 0 0);
  --secondary-foreground: oklch(0.985 0 0);
  --muted: oklch(0.269 0 0);
  --muted-foreground: oklch(0.708 0 0);
  --accent: oklch(0.269 0 0);
  --accent-foreground: oklch(0.985 0 0);
  --destructive: oklch(0.704 0.191 22.216);
  --destructive-foreground: oklch(0.985 0 0);
  --border: oklch(0.269 0 0);
  --input: oklch(0.269 0 0);
  --ring: oklch(0.623 0.214 259.815);
}

@theme inline {
  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-card: var(--card);
  --color-card-foreground: var(--card-foreground);
  --color-popover: var(--popover);
  --color-popover-foreground: var(--popover-foreground);
  --color-primary: var(--primary);
  --color-primary-foreground: var(--primary-foreground);
  --color-secondary: var(--secondary);
  --color-secondary-foreground: var(--secondary-foreground);
  --color-muted: var(--muted);
  --color-muted-foreground: var(--muted-foreground);
  --color-accent: var(--accent);
  --color-accent-foreground: var(--accent-foreground);
  --color-destructive: var(--destructive);
  --color-destructive-foreground: var(--destructive-foreground);
  --color-border: var(--border);
  --color-input: var(--input);
  --color-ring: var(--ring);
  --radius-sm: calc(var(--radius) - 4px);
  --radius-md: calc(var(--radius) - 2px);
  --radius-lg: var(--radius);
  --radius-xl: calc(var(--radius) + 4px);
}

@layer base {
  * {
    @apply border-border;
  }
  body {
    @apply bg-background text-foreground;
    font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  }
}
```

- [ ] **Step 6: Set up utility library**

Write `website/src/lib/utils.ts`:

```typescript
import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
```

- [ ] **Step 7: Create placeholder root layout**

Write `website/src/app/layout.tsx`:

```tsx
import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Courier MFT — Open Source Managed File Transfer",
  description:
    "Enterprise file transfer platform. Replace SFTP scripts, PGP workflows, and cron jobs with a single auditable platform.",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>{children}</body>
    </html>
  );
}
```

- [ ] **Step 8: Create placeholder homepage**

Write `website/src/app/page.tsx`:

```tsx
export default function HomePage() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center">
      <h1 className="text-4xl font-bold">Courier MFT</h1>
      <p className="mt-4 text-muted-foreground">Coming soon</p>
    </main>
  );
}
```

- [ ] **Step 9: Update root .gitignore**

Append to `.gitignore`:

```gitignore
# Website build artifacts
website/docs-content/
website/out/
.superpowers/
```

- [ ] **Step 10: Install dependencies and verify build**

```bash
cd website && npm install
```

Then verify the dev server starts:
```bash
npm run dev
```

Expected: Next.js dev server starts at localhost:3000 showing "Courier MFT / Coming soon".

Kill the dev server, then verify static export works:
```bash
npm run build 2>&1 || true
```

Expected: Build may warn about missing sync scripts (prebuild), but `next build` should succeed and produce `website/out/` with static HTML. The prebuild script errors are expected — we haven't written sync-docs.ts and sync-screenshots.ts yet.

- [ ] **Step 11: Initialize shadcn/ui**

```bash
cd website && npx shadcn@latest init
```

When prompted:
- Style: New York
- Base color: Neutral
- CSS variables: Yes

This creates `website/components.json` and updates globals.css if needed.

- [ ] **Step 12: Add shadcn/ui components we'll need**

```bash
cd website && npx shadcn@latest add button card badge input separator sheet scroll-area dialog
```

- [ ] **Step 13: Commit scaffold**

```bash
git add website/ .gitignore
git commit -m "feat(website): scaffold Next.js project with Tailwind, shadcn/ui"
```

---

### Task 2: Theme Provider + Header + Footer

**Files:**
- Create: `website/src/components/layout/theme-provider.tsx`
- Create: `website/src/components/layout/theme-toggle.tsx`
- Create: `website/src/components/layout/header.tsx`
- Create: `website/src/components/layout/footer.tsx`
- Modify: `website/src/app/layout.tsx`

- [ ] **Step 1: Create ThemeProvider wrapper**

Write `website/src/components/layout/theme-provider.tsx`:

```tsx
"use client";

import { ThemeProvider as NextThemesProvider } from "next-themes";

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  return (
    <NextThemesProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      disableTransitionOnChange
    >
      {children}
    </NextThemesProvider>
  );
}
```

- [ ] **Step 2: Create ThemeToggle component**

Write `website/src/components/layout/theme-toggle.tsx`:

```tsx
"use client";

import { Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";
import { Button } from "@/components/ui/button";
import { useEffect, useState } from "react";

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);

  useEffect(() => setMounted(true), []);

  if (!mounted) {
    return <Button variant="ghost" size="icon" className="h-9 w-9" />;
  }

  return (
    <Button
      variant="ghost"
      size="icon"
      className="h-9 w-9"
      onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
    >
      <Sun className="h-4 w-4 rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0" />
      <Moon className="absolute h-4 w-4 rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100" />
      <span className="sr-only">Toggle theme</span>
    </Button>
  );
}
```

- [ ] **Step 3: Create Header component**

Write `website/src/components/layout/header.tsx`:

```tsx
"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Package, Menu } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTrigger } from "@/components/ui/sheet";
import { ThemeToggle } from "./theme-toggle";
import { cn } from "@/lib/utils";
import { useState } from "react";

const navItems = [
  { label: "Docs", href: "/docs" },
  { label: "Screenshots", href: "/screenshots" },
];

export function Header() {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  return (
    <header className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className="mx-auto flex h-14 max-w-7xl items-center px-4 sm:px-6 lg:px-8">
        <Link href="/" className="mr-6 flex items-center gap-2 font-bold">
          <Package className="h-5 w-5 text-primary" />
          <span>Courier MFT</span>
        </Link>

        {/* Desktop nav */}
        <nav className="hidden items-center gap-6 text-sm md:flex">
          {navItems.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "transition-colors hover:text-foreground",
                pathname?.startsWith(item.href)
                  ? "text-foreground font-medium"
                  : "text-muted-foreground"
              )}
            >
              {item.label}
            </Link>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-2">
          <ThemeToggle />
          <Button variant="outline" size="sm" asChild className="hidden sm:flex">
            <a
              href="https://github.com/Battle-Line-Productions/Courier"
              target="_blank"
              rel="noopener noreferrer"
            >
              GitHub
            </a>
          </Button>

          {/* Mobile menu */}
          <Sheet open={open} onOpenChange={setOpen}>
            <SheetTrigger asChild className="md:hidden">
              <Button variant="ghost" size="icon" className="h-9 w-9">
                <Menu className="h-4 w-4" />
                <span className="sr-only">Menu</span>
              </Button>
            </SheetTrigger>
            <SheetContent side="right" className="w-[240px]">
              <nav className="flex flex-col gap-4 pt-8">
                {navItems.map((item) => (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={() => setOpen(false)}
                    className={cn(
                      "text-sm transition-colors hover:text-foreground",
                      pathname?.startsWith(item.href)
                        ? "text-foreground font-medium"
                        : "text-muted-foreground"
                    )}
                  >
                    {item.label}
                  </Link>
                ))}
                <a
                  href="https://github.com/Battle-Line-Productions/Courier"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                >
                  GitHub
                </a>
              </nav>
            </SheetContent>
          </Sheet>
        </div>
      </div>
    </header>
  );
}
```

- [ ] **Step 4: Create Footer component**

Write `website/src/components/layout/footer.tsx`:

```tsx
import Link from "next/link";

const footerLinks = {
  Product: [
    { label: "Documentation", href: "/docs" },
    { label: "Screenshots", href: "/screenshots" },
    { label: "Getting Started", href: "/docs/getting-started" },
  ],
  Community: [
    {
      label: "GitHub",
      href: "https://github.com/Battle-Line-Productions/Courier",
      external: true,
    },
    {
      label: "Contributing",
      href: "/docs/contributing",
    },
    {
      label: "Issues",
      href: "https://github.com/Battle-Line-Productions/Courier/issues",
      external: true,
    },
  ],
  Legal: [
    {
      label: "License (Apache 2.0)",
      href: "https://github.com/Battle-Line-Productions/Courier/blob/main/LICENSE",
      external: true,
    },
    { label: "Security", href: "/docs/security" },
    { label: "Privacy Policy", href: "/privacy" },
  ],
};

export function Footer() {
  return (
    <footer className="border-t bg-muted/40">
      <div className="mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
        <div className="grid grid-cols-2 gap-8 md:grid-cols-3">
          {Object.entries(footerLinks).map(([category, links]) => (
            <div key={category}>
              <h3 className="text-sm font-semibold">{category}</h3>
              <ul className="mt-3 space-y-2">
                {links.map((link) => (
                  <li key={link.href}>
                    {link.external ? (
                      <a
                        href={link.href}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                      >
                        {link.label}
                      </a>
                    ) : (
                      <Link
                        href={link.href}
                        className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                      >
                        {link.label}
                      </Link>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
        <div className="mt-8 border-t pt-8 text-center text-sm text-muted-foreground">
          <p>
            &copy; {new Date().getFullYear()} Battle Line Productions. Released
            under the Apache 2.0 License.
          </p>
        </div>
      </div>
    </footer>
  );
}
```

- [ ] **Step 5: Wire layout with ThemeProvider, Header, Footer**

Update `website/src/app/layout.tsx`:

```tsx
import type { Metadata } from "next";
import { ThemeProvider } from "@/components/layout/theme-provider";
import { Header } from "@/components/layout/header";
import { Footer } from "@/components/layout/footer";
import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "Courier MFT — Open Source Managed File Transfer",
    template: "%s | Courier MFT",
  },
  description:
    "Enterprise file transfer platform. Replace SFTP scripts, PGP workflows, and cron jobs with a single auditable platform. Open source, self-hosted.",
  keywords: [
    "managed file transfer",
    "open source MFT",
    "enterprise file transfer",
    "SFTP automation",
    "PGP encryption",
    "file transfer orchestration",
  ],
  metadataBase: new URL("https://couriermft.com"),
  openGraph: {
    type: "website",
    locale: "en_US",
    url: "https://couriermft.com",
    siteName: "Courier MFT",
    title: "Courier MFT — Open Source Managed File Transfer",
    description:
      "Enterprise file transfer platform. Replace SFTP scripts, PGP workflows, and cron jobs with a single auditable platform.",
  },
  twitter: {
    card: "summary_large_image",
    title: "Courier MFT — Open Source Managed File Transfer",
    description:
      "Enterprise file transfer platform. Replace SFTP scripts, PGP workflows, and cron jobs with a single auditable platform.",
  },
  robots: {
    index: true,
    follow: true,
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className="min-h-screen bg-background antialiased">
        <ThemeProvider>
          <div className="relative flex min-h-screen flex-col">
            <Header />
            <main className="flex-1">{children}</main>
            <Footer />
          </div>
        </ThemeProvider>
      </body>
    </html>
  );
}
```

- [ ] **Step 6: Verify theme toggle works**

```bash
cd website && npm run dev
```

Open http://localhost:3000. Verify:
- Header shows with "Courier MFT" logo, nav links, theme toggle, GitHub button
- Footer renders with 3 columns of links
- Clicking sun/moon icon toggles light/dark mode
- Mobile: hamburger menu appears and works

- [ ] **Step 7: Commit**

```bash
git add website/src/components/layout/ website/src/app/layout.tsx
git commit -m "feat(website): add layout with header, footer, and dark mode toggle"
```

---

## Phase 2: Build Pipeline (Docs + Screenshots Sync)

### Task 3: sync-docs.ts — Split Design Doc into Pages

**Files:**
- Create: `website/scripts/sync-docs.ts`
- Create: `website/docs-content/` (build artifact, gitignored)

- [ ] **Step 1: Write the sync-docs script**

Write `website/scripts/sync-docs.ts`:

```typescript
import * as fs from "fs";
import * as path from "path";

const REPO_ROOT = path.resolve(__dirname, "../..");
const DESIGN_DOC = path.join(REPO_ROOT, "Docs/CourierDesignDoc.md");
const README = path.join(REPO_ROOT, "README.md");
const CONTRIBUTING = path.join(REPO_ROOT, "CONTRIBUTING.md");
const OUTPUT_DIR = path.join(__dirname, "../docs-content");

interface DocPage {
  slug: string;
  title: string;
  description: string;
  order: number;
  keywords: string[];
  content: string;
}

const SECTION_MAP: Record<
  string,
  { slug: string; description: string; keywords: string[] }
> = {
  "Executive Summary": {
    slug: "overview",
    description:
      "What Courier MFT is, the problems it solves, and why it exists.",
    keywords: [
      "managed file transfer",
      "open source MFT",
      "enterprise file transfer",
      "MFT alternative",
    ],
  },
  "Architecture Overview": {
    slug: "architecture",
    description:
      "System architecture, deployment units, and dependency layers for Courier MFT.",
    keywords: [
      "MFT architecture",
      "file transfer platform design",
      "vertical slice architecture",
    ],
  },
  "Tech Stack": {
    slug: "tech-stack",
    description:
      "Technologies and libraries used in Courier MFT — .NET, PostgreSQL, Next.js, and more.",
    keywords: [
      "MFT tech stack",
      ".NET file transfer",
      "PostgreSQL MFT",
    ],
  },
  "Domain Model": {
    slug: "domain-model",
    description:
      "Entities, value objects, enums, and relationships in Courier's domain model.",
    keywords: [
      "MFT domain model",
      "file transfer entities",
      "job scheduling model",
    ],
  },
  "Job Engine Design": {
    slug: "jobs",
    description:
      "Multi-step job pipelines — define, schedule, and execute file transfer workflows.",
    keywords: [
      "file transfer job engine",
      "pipeline orchestration",
      "step execution",
      "job scheduler",
    ],
  },
  "Connection & Protocol Layer": {
    slug: "connections",
    description:
      "Manage SFTP, FTP, local filesystem, and Azure Function connections securely.",
    keywords: [
      "SFTP connection management",
      "FTP automation",
      "secure file transfer connections",
    ],
  },
  "Cryptography & Key Store": {
    slug: "encryption",
    description:
      "AES-256-GCM envelope encryption, PGP key management, and Azure Key Vault integration.",
    keywords: [
      "PGP encryption automation",
      "AES-256 file transfer",
      "key management MFT",
    ],
  },
  "File Operations": {
    slug: "file-operations",
    description:
      "File copy, move, compression, and transformation operations within job pipelines.",
    keywords: [
      "file compression automation",
      "file operations pipeline",
    ],
  },
  "File Monitor System": {
    slug: "monitors",
    description:
      "Watch directories and trigger jobs automatically when files arrive.",
    keywords: [
      "file monitor system",
      "directory watch triggers",
      "automated file detection",
    ],
  },
  "API Design": {
    slug: "api",
    description:
      "RESTful API for managing jobs, connections, keys, and system configuration.",
    keywords: [
      "file transfer REST API",
      "MFT API",
      "managed file transfer API",
    ],
  },
  "Frontend Architecture": {
    slug: "frontend",
    description:
      "Next.js dashboard for managing all file transfer operations through a modern UI.",
    keywords: ["MFT dashboard", "file transfer UI", "management console"],
  },
  Security: {
    slug: "security",
    description:
      "Authentication, authorization, RBAC, encryption at rest, and audit logging.",
    keywords: [
      "secure file transfer",
      "RBAC file transfer",
      "MFT security",
      "audit logging",
    ],
  },
  "Database Schema": {
    slug: "database",
    description:
      "PostgreSQL schema design — tables, indexes, and migration strategy.",
    keywords: [
      "MFT database schema",
      "file transfer database design",
    ],
  },
  "Deployment & Infrastructure": {
    slug: "deployment",
    description:
      "Deploy Courier with Docker, Aspire, or bare metal. CI/CD pipeline configuration.",
    keywords: [
      "deploy MFT",
      "docker file transfer",
      "self-hosted MFT",
      "MFT deployment",
    ],
  },
};

function splitDesignDoc(content: string): DocPage[] {
  const pages: DocPage[] = [];
  // Match H1 headers like "# 1. Executive Summary" or "# 12. Security"
  const sectionRegex = /^# \d+\.\s+(.+)$/gm;
  const matches: { title: string; index: number }[] = [];

  let match: RegExpExecArray | null;
  while ((match = sectionRegex.exec(content)) !== null) {
    matches.push({ title: match[1].trim(), index: match.index });
  }

  for (let i = 0; i < matches.length; i++) {
    const { title, index } = matches[i];
    const endIndex = i + 1 < matches.length ? matches[i + 1].index : content.length;
    const sectionContent = content.slice(index, endIndex).trim();

    const mapping = SECTION_MAP[title];
    if (!mapping) {
      console.warn(`No mapping for section: "${title}" — skipping`);
      continue;
    }

    pages.push({
      slug: mapping.slug,
      title,
      description: mapping.description,
      order: pages.length + 2, // 1 is reserved for getting-started
      keywords: mapping.keywords,
      // Remove the H1 header line (we'll use the title from frontmatter)
      content: sectionContent.replace(/^# .+\n+/, ""),
    });
  }

  return pages;
}

function buildGettingStarted(readmeContent: string): DocPage {
  // Extract from "## Quick Start" through the end of useful setup content
  const quickStartMatch = readmeContent.match(
    /## (?:Quick Start|Getting Started)[\s\S]*?(?=## (?:Architecture|Documentation|Contributing|Roadmap|License)|$)/i
  );

  const content = quickStartMatch
    ? quickStartMatch[0]
    : "## Getting Started\n\nSee the [README](https://github.com/Battle-Line-Productions/Courier) for setup instructions.";

  return {
    slug: "getting-started",
    title: "Getting Started",
    description:
      "Install and run Courier MFT locally with Docker and Aspire in minutes.",
    order: 1,
    keywords: [
      "install courier",
      "self-hosted MFT setup",
      "getting started MFT",
      "docker file transfer setup",
    ],
    content,
  };
}

function buildContributing(contributingContent: string): DocPage {
  return {
    slug: "contributing",
    title: "Contributing",
    description:
      "How to contribute to Courier MFT — bug reports, feature requests, and pull requests.",
    order: 99,
    keywords: [
      "contribute open source MFT",
      "courier contributing guide",
    ],
    content: contributingContent,
  };
}

function writeDocPage(page: DocPage): void {
  const frontmatter = `---
title: "${page.title}"
description: "${page.description}"
order: ${page.order}
keywords: ${JSON.stringify(page.keywords)}
---`;

  const output = `${frontmatter}\n\n${page.content}\n`;
  const outputPath = path.join(OUTPUT_DIR, `${page.slug}.md`);
  fs.writeFileSync(outputPath, output, "utf-8");
  console.log(`  wrote ${page.slug}.md`);
}

// Main
console.log("sync-docs: splitting design doc into pages...");

// Clean output dir
if (fs.existsSync(OUTPUT_DIR)) {
  fs.rmSync(OUTPUT_DIR, { recursive: true });
}
fs.mkdirSync(OUTPUT_DIR, { recursive: true });

// Split design doc
const designDoc = fs.readFileSync(DESIGN_DOC, "utf-8");
const pages = splitDesignDoc(designDoc);

// Add getting-started from README
const readme = fs.readFileSync(README, "utf-8");
pages.unshift(buildGettingStarted(readme));

// Add contributing
const contributing = fs.readFileSync(CONTRIBUTING, "utf-8");
pages.push(buildContributing(contributing));

// Write all pages
for (const page of pages) {
  writeDocPage(page);
}

console.log(`sync-docs: wrote ${pages.length} pages to docs-content/`);
```

- [ ] **Step 2: Run the script and verify output**

```bash
cd website && npx tsx scripts/sync-docs.ts
```

Expected output:
```
sync-docs: splitting design doc into pages...
  wrote getting-started.md
  wrote overview.md
  wrote architecture.md
  wrote tech-stack.md
  wrote domain-model.md
  wrote jobs.md
  wrote connections.md
  wrote encryption.md
  wrote file-operations.md
  wrote monitors.md
  wrote api.md
  wrote frontend.md
  wrote security.md
  wrote database.md
  wrote deployment.md
  wrote contributing.md
sync-docs: wrote 16 pages to docs-content/
```

Verify a sample file has correct frontmatter:
```bash
head -10 website/docs-content/jobs.md
```

Expected: frontmatter block with title, description, order, keywords, followed by the section content.

- [ ] **Step 3: Commit**

```bash
git add website/scripts/sync-docs.ts
git commit -m "feat(website): add sync-docs script to split design doc into pages"
```

---

### Task 4: sync-screenshots.ts — Copy Screenshots

**Files:**
- Create: `website/scripts/sync-screenshots.ts`

- [ ] **Step 1: Write the sync-screenshots script**

Write `website/scripts/sync-screenshots.ts`:

```typescript
import * as fs from "fs";
import * as path from "path";

const REPO_ROOT = path.resolve(__dirname, "../..");
const SOURCE_DIR = path.join(
  REPO_ROOT,
  "src/Courier.Frontend/public/guide/screenshots"
);
const OUTPUT_DIR = path.join(__dirname, "../public/screenshots");

console.log("sync-screenshots: copying screenshots...");

// Clean output dir
if (fs.existsSync(OUTPUT_DIR)) {
  fs.rmSync(OUTPUT_DIR, { recursive: true });
}
fs.mkdirSync(OUTPUT_DIR, { recursive: true });

// Copy all image files
const files = fs.readdirSync(SOURCE_DIR).filter((f) => /\.(png|jpg|jpeg|gif|webp)$/i.test(f));

for (const file of files) {
  fs.copyFileSync(path.join(SOURCE_DIR, file), path.join(OUTPUT_DIR, file));
}

console.log(`sync-screenshots: copied ${files.length} screenshots to public/screenshots/`);
```

- [ ] **Step 2: Run and verify**

```bash
cd website && npx tsx scripts/sync-screenshots.ts
```

Expected: `sync-screenshots: copied 33 screenshots to public/screenshots/`

Verify files exist:
```bash
ls website/public/screenshots/ | head -5
```

- [ ] **Step 3: Commit**

```bash
git add website/scripts/sync-screenshots.ts
git commit -m "feat(website): add sync-screenshots script"
```

---

### Task 5: Docs Library — Read & Parse Markdown

**Files:**
- Create: `website/src/lib/docs.ts`
- Create: `website/src/lib/keywords.ts`
- Create: `website/src/lib/metadata.ts`

- [ ] **Step 1: Write keywords.ts**

Write `website/src/lib/keywords.ts`:

```typescript
export interface DocKeywords {
  title: string;
  description: string;
  keywords: string[];
}

/**
 * Fallback keyword map for doc slugs.
 * Primary source is frontmatter in the generated markdown files.
 * This map provides defaults if frontmatter is missing or for static pages.
 */
export const PAGE_KEYWORDS: Record<string, DocKeywords> = {
  home: {
    title: "Open Source Managed File Transfer",
    description:
      "Enterprise file transfer platform. Replace SFTP scripts, PGP workflows, and cron jobs with a single auditable platform. Open source, self-hosted.",
    keywords: [
      "managed file transfer",
      "open source MFT",
      "enterprise file transfer",
      "SFTP automation",
      "PGP encryption tool",
      "file transfer orchestration",
      "secure file transfer platform",
      "self-hosted file transfer",
      "MFT alternative to GoAnywhere",
      "free managed file transfer software",
    ],
  },
  screenshots: {
    title: "Screenshots",
    description:
      "See Courier MFT in action — dashboard, job builder, connections, encryption keys, monitoring, and more.",
    keywords: [
      "MFT dashboard",
      "file transfer UI",
      "managed file transfer screenshots",
    ],
  },
};
```

- [ ] **Step 2: Write docs.ts**

Write `website/src/lib/docs.ts`:

```typescript
import fs from "fs";
import path from "path";
import matter from "gray-matter";

const DOCS_DIR = path.join(process.cwd(), "docs-content");

export interface DocPage {
  slug: string;
  title: string;
  description: string;
  order: number;
  keywords: string[];
  content: string;
  headings: DocHeading[];
}

export interface DocHeading {
  depth: number;
  text: string;
  id: string;
}

function extractHeadings(content: string): DocHeading[] {
  const headingRegex = /^(#{2,4})\s+(.+)$/gm;
  const headings: DocHeading[] = [];
  let match: RegExpExecArray | null;

  while ((match = headingRegex.exec(content)) !== null) {
    const text = match[2].trim();
    headings.push({
      depth: match[1].length,
      text,
      id: text
        .toLowerCase()
        .replace(/[^\w\s-]/g, "")
        .replace(/\s+/g, "-"),
    });
  }

  return headings;
}

function slugify(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^\w\s-]/g, "")
    .replace(/\s+/g, "-");
}

export function getAllDocs(): DocPage[] {
  if (!fs.existsSync(DOCS_DIR)) {
    console.warn("docs-content/ not found — run sync-docs first");
    return [];
  }

  const files = fs.readdirSync(DOCS_DIR).filter((f) => f.endsWith(".md"));

  const docs = files.map((file) => {
    const raw = fs.readFileSync(path.join(DOCS_DIR, file), "utf-8");
    const { data, content } = matter(raw);

    return {
      slug: file.replace(/\.md$/, ""),
      title: data.title || file.replace(/\.md$/, ""),
      description: data.description || "",
      order: data.order || 99,
      keywords: data.keywords || [],
      content,
      headings: extractHeadings(content),
    } satisfies DocPage;
  });

  return docs.sort((a, b) => a.order - b.order);
}

export function getDocBySlug(slug: string): DocPage | undefined {
  const docs = getAllDocs();
  return docs.find((d) => d.slug === slug);
}

export function getDocSlugs(): string[] {
  return getAllDocs().map((d) => d.slug);
}

export interface SidebarSection {
  title: string;
  slug: string;
}

export function getDocsSidebar(): SidebarSection[] {
  return getAllDocs().map((d) => ({
    title: d.title,
    slug: d.slug,
  }));
}
```

- [ ] **Step 3: Write metadata.ts**

Write `website/src/lib/metadata.ts`:

```typescript
import type { Metadata } from "next";
import { PAGE_KEYWORDS } from "./keywords";

const SITE_URL = "https://couriermft.com";
const SITE_NAME = "Courier MFT";

interface MetadataOptions {
  title: string;
  description: string;
  keywords?: string[];
  path?: string;
}

export function buildMetadata({
  title,
  description,
  keywords = [],
  path = "",
}: MetadataOptions): Metadata {
  const url = `${SITE_URL}${path}`;

  return {
    title,
    description,
    keywords,
    alternates: { canonical: url },
    openGraph: {
      title: `${title} | ${SITE_NAME}`,
      description,
      url,
      siteName: SITE_NAME,
      type: "website",
    },
    twitter: {
      card: "summary_large_image",
      title: `${title} | ${SITE_NAME}`,
      description,
    },
  };
}

export function buildDocMetadata(
  slug: string,
  frontmatter: { title: string; description: string; keywords: string[] }
): Metadata {
  return buildMetadata({
    title: frontmatter.title,
    description: frontmatter.description,
    keywords: frontmatter.keywords,
    path: `/docs/${slug}`,
  });
}

export { PAGE_KEYWORDS, SITE_URL, SITE_NAME };
```

- [ ] **Step 4: Commit**

```bash
git add website/src/lib/docs.ts website/src/lib/keywords.ts website/src/lib/metadata.ts
git commit -m "feat(website): add docs library, keyword map, and metadata factory"
```

---

## Phase 3: Homepage

### Task 6: Homepage Components

**Files:**
- Create: `website/src/components/home/hero.tsx`
- Create: `website/src/components/home/features.tsx`
- Create: `website/src/components/home/quick-start.tsx`
- Create: `website/src/components/home/screenshot-showcase.tsx`
- Create: `website/src/components/home/tech-stack.tsx`
- Modify: `website/src/app/page.tsx`

- [ ] **Step 1: Create Hero component**

Write `website/src/components/home/hero.tsx`:

```tsx
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ArrowRight, Github } from "lucide-react";

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
                <Github className="mr-2 h-4 w-4" />
                GitHub
              </a>
            </Button>
          </div>
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 2: Create Features component**

Write `website/src/components/home/features.tsx`:

```tsx
import {
  ArrowRightLeft,
  FileKey,
  CalendarClock,
  ShieldCheck,
  Activity,
  Link as LinkIcon,
  FolderSearch,
  Zap,
} from "lucide-react";

const features = [
  {
    icon: ArrowRightLeft,
    title: "Multi-Step Job Pipelines",
    description:
      "Chain file operations, transfers, encryption, and custom steps into automated workflows.",
  },
  {
    icon: LinkIcon,
    title: "SFTP, FTP & Local Connections",
    description:
      "Manage connections to remote servers with credential encryption and connection pooling.",
  },
  {
    icon: FileKey,
    title: "PGP & AES-256 Encryption",
    description:
      "Encrypt and decrypt files with PGP keys or AES-256-GCM. Full key lifecycle management.",
  },
  {
    icon: CalendarClock,
    title: "Scheduling & Triggers",
    description:
      "Run jobs on cron schedules or trigger them via API. Quartz.NET persistent scheduler.",
  },
  {
    icon: ShieldCheck,
    title: "RBAC & Audit Trail",
    description:
      "Role-based access control with Admin, Operator, and Viewer roles. Every action logged.",
  },
  {
    icon: Activity,
    title: "Monitoring & Alerts",
    description:
      "Watch directories for new files. Get notified on job failures via configurable notifications.",
  },
  {
    icon: FolderSearch,
    title: "File Operations",
    description:
      "Copy, move, compress, and transform files as pipeline steps with context passing between steps.",
  },
  {
    icon: Zap,
    title: "Azure Function Integration",
    description:
      "Trigger Azure Functions as pipeline steps with callback support for async workflows.",
  },
];

export function Features() {
  return (
    <section className="border-t bg-muted/30 py-20">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="text-3xl font-bold tracking-tight">
            Everything You Need for Managed File Transfer
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            A complete platform for automating, securing, and monitoring file
            transfer operations.
          </p>
        </div>
        <div className="mt-16 grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((feature) => (
            <div
              key={feature.title}
              className="group rounded-lg border bg-card p-6 transition-colors hover:border-primary/50"
            >
              <feature.icon className="h-8 w-8 text-primary" />
              <h3 className="mt-4 font-semibold">{feature.title}</h3>
              <p className="mt-2 text-sm text-muted-foreground">
                {feature.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 3: Create QuickStart component**

Write `website/src/components/home/quick-start.tsx`:

```tsx
export function QuickStart() {
  return (
    <section className="py-20">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="text-3xl font-bold tracking-tight">
            Up and Running in Minutes
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            Clone the repo, start with Docker, and you're ready to go.
          </p>
        </div>
        <div className="mx-auto mt-12 max-w-2xl">
          <div className="overflow-hidden rounded-lg border bg-card">
            <div className="flex items-center gap-2 border-b bg-muted/50 px-4 py-3">
              <div className="h-3 w-3 rounded-full bg-red-500" />
              <div className="h-3 w-3 rounded-full bg-yellow-500" />
              <div className="h-3 w-3 rounded-full bg-green-500" />
              <span className="ml-2 text-xs text-muted-foreground">
                Terminal
              </span>
            </div>
            <pre className="overflow-x-auto p-6 text-sm">
              <code>
                <span className="text-muted-foreground">
                  # Clone the repository
                </span>
                {"\n"}
                <span className="text-primary">$</span> git clone
                https://github.com/Battle-Line-Productions/Courier.git
                {"\n"}
                <span className="text-primary">$</span> cd Courier{"\n"}
                {"\n"}
                <span className="text-muted-foreground">
                  # Install frontend dependencies
                </span>
                {"\n"}
                <span className="text-primary">$</span> cd
                src/Courier.Frontend && npm install{"\n"}
                {"\n"}
                <span className="text-muted-foreground">
                  # Start everything with Aspire (API, Worker, Frontend, Postgres, Seq)
                </span>
                {"\n"}
                <span className="text-primary">$</span> cd
                ../Courier.AppHost && dotnet run{"\n"}
                {"\n"}
                <span className="text-green-600 dark:text-green-400">
                  ✓ Dashboard ready at http://localhost:5000
                </span>
              </code>
            </pre>
          </div>
          <p className="mt-4 text-center text-sm text-muted-foreground">
            Prerequisites: .NET 10 SDK, Docker Desktop, Node.js 20+
          </p>
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Create ScreenshotShowcase component**

Write `website/src/components/home/screenshot-showcase.tsx`:

```tsx
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
          <h2 className="text-3xl font-bold tracking-tight">See It in Action</h2>
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
```

- [ ] **Step 5: Create TechStack component**

Write `website/src/components/home/tech-stack.tsx`:

```tsx
const technologies = [
  { name: ".NET 10", category: "Backend" },
  { name: "ASP.NET Core", category: "API" },
  { name: "PostgreSQL 16", category: "Database" },
  { name: "Next.js", category: "Frontend" },
  { name: "React 19", category: "UI" },
  { name: "Docker", category: "Deployment" },
  { name: "Quartz.NET", category: "Scheduling" },
  { name: "BouncyCastle", category: "Cryptography" },
];

export function TechStack() {
  return (
    <section className="py-20">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="text-3xl font-bold tracking-tight">
            Built on Proven Technology
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            Enterprise-grade stack you already know and trust.
          </p>
        </div>
        <div className="mx-auto mt-12 flex max-w-3xl flex-wrap items-center justify-center gap-4">
          {technologies.map((tech) => (
            <div
              key={tech.name}
              className="flex items-center gap-2 rounded-full border bg-card px-4 py-2"
            >
              <span className="text-sm font-medium">{tech.name}</span>
              <span className="text-xs text-muted-foreground">
                {tech.category}
              </span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 6: Assemble the homepage**

Update `website/src/app/page.tsx`:

```tsx
import { Hero } from "@/components/home/hero";
import { Features } from "@/components/home/features";
import { QuickStart } from "@/components/home/quick-start";
import { ScreenshotShowcase } from "@/components/home/screenshot-showcase";
import { TechStack } from "@/components/home/tech-stack";

export default function HomePage() {
  return (
    <>
      <Hero />
      <Features />
      <ScreenshotShowcase />
      <QuickStart />
      <TechStack />
    </>
  );
}
```

- [ ] **Step 7: Run sync scripts then verify homepage**

```bash
cd website && npx tsx scripts/sync-docs.ts && npx tsx scripts/sync-screenshots.ts && npm run dev
```

Open http://localhost:3000. Verify all sections render: Hero with CTAs, feature grid (8 cards), screenshot showcase (4 images), quick start terminal, tech stack badges. Test dark mode toggle.

- [ ] **Step 8: Commit**

```bash
git add website/src/components/home/ website/src/app/page.tsx
git commit -m "feat(website): add homepage with hero, features, screenshots, quick start, tech stack"
```

---

## Phase 4: Docs Pages

### Task 7: MDX Components + Docs Layout

**Files:**
- Create: `website/src/components/docs/mdx-components.tsx`
- Create: `website/src/components/docs/docs-sidebar.tsx`
- Create: `website/src/components/docs/table-of-contents.tsx`
- Create: `website/src/app/docs/layout.tsx`

- [ ] **Step 1: Create MDX custom components**

Write `website/src/components/docs/mdx-components.tsx`:

```tsx
import type { MDXComponents } from "mdx/types";

function slugify(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^\w\s-]/g, "")
    .replace(/\s+/g, "-");
}

export const mdxComponents: MDXComponents = {
  h1: ({ children, ...props }) => (
    <h1
      id={typeof children === "string" ? slugify(children) : undefined}
      className="mt-8 scroll-mt-20 text-3xl font-bold tracking-tight"
      {...props}
    >
      {children}
    </h1>
  ),
  h2: ({ children, ...props }) => (
    <h2
      id={typeof children === "string" ? slugify(children) : undefined}
      className="mt-8 scroll-mt-20 border-b pb-2 text-2xl font-semibold tracking-tight"
      {...props}
    >
      {children}
    </h2>
  ),
  h3: ({ children, ...props }) => (
    <h3
      id={typeof children === "string" ? slugify(children) : undefined}
      className="mt-6 scroll-mt-20 text-xl font-semibold tracking-tight"
      {...props}
    >
      {children}
    </h3>
  ),
  h4: ({ children, ...props }) => (
    <h4
      id={typeof children === "string" ? slugify(children) : undefined}
      className="mt-4 scroll-mt-20 text-lg font-semibold tracking-tight"
      {...props}
    >
      {children}
    </h4>
  ),
  p: ({ children, ...props }) => (
    <p className="mt-4 leading-7" {...props}>
      {children}
    </p>
  ),
  ul: ({ children, ...props }) => (
    <ul className="mt-4 list-disc pl-6 leading-7" {...props}>
      {children}
    </ul>
  ),
  ol: ({ children, ...props }) => (
    <ol className="mt-4 list-decimal pl-6 leading-7" {...props}>
      {children}
    </ol>
  ),
  li: ({ children, ...props }) => (
    <li className="mt-1" {...props}>
      {children}
    </li>
  ),
  code: ({ children, ...props }) => (
    <code
      className="rounded bg-muted px-1.5 py-0.5 text-sm font-mono"
      {...props}
    >
      {children}
    </code>
  ),
  pre: ({ children, ...props }) => (
    <pre
      className="mt-4 overflow-x-auto rounded-lg border bg-muted/50 p-4 text-sm"
      {...props}
    >
      {children}
    </pre>
  ),
  table: ({ children, ...props }) => (
    <div className="mt-4 overflow-x-auto">
      <table className="w-full border-collapse text-sm" {...props}>
        {children}
      </table>
    </div>
  ),
  th: ({ children, ...props }) => (
    <th
      className="border-b bg-muted/50 px-4 py-2 text-left font-semibold"
      {...props}
    >
      {children}
    </th>
  ),
  td: ({ children, ...props }) => (
    <td className="border-b px-4 py-2" {...props}>
      {children}
    </td>
  ),
  blockquote: ({ children, ...props }) => (
    <blockquote
      className="mt-4 border-l-4 border-primary/50 pl-4 italic text-muted-foreground"
      {...props}
    >
      {children}
    </blockquote>
  ),
  a: ({ href, children, ...props }) => (
    <a
      href={href}
      className="text-primary underline underline-offset-4 hover:text-primary/80"
      {...(href?.startsWith("http")
        ? { target: "_blank", rel: "noopener noreferrer" }
        : {})}
      {...props}
    >
      {children}
    </a>
  ),
  hr: () => <hr className="my-8 border-border" />,
  img: ({ src, alt, ...props }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={src}
      alt={alt || ""}
      className="mt-4 rounded-lg border"
      loading="lazy"
      {...props}
    />
  ),
};
```

- [ ] **Step 2: Create DocsSidebar component**

Write `website/src/components/docs/docs-sidebar.tsx`:

```tsx
"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import type { SidebarSection } from "@/lib/docs";

interface DocsSidebarProps {
  sections: SidebarSection[];
}

export function DocsSidebar({ sections }: DocsSidebarProps) {
  const pathname = usePathname();

  return (
    <nav className="space-y-1">
      <h4 className="mb-3 text-sm font-semibold">Documentation</h4>
      {sections.map((section) => {
        const href = `/docs/${section.slug}`;
        const isActive = pathname === href;

        return (
          <Link
            key={section.slug}
            href={href}
            className={cn(
              "block rounded-md px-3 py-2 text-sm transition-colors",
              isActive
                ? "bg-primary/10 font-medium text-primary"
                : "text-muted-foreground hover:bg-muted hover:text-foreground"
            )}
          >
            {section.title}
          </Link>
        );
      })}
    </nav>
  );
}
```

- [ ] **Step 3: Create TableOfContents component**

Write `website/src/components/docs/table-of-contents.tsx`:

```tsx
"use client";

import { cn } from "@/lib/utils";
import { useEffect, useState } from "react";
import type { DocHeading } from "@/lib/docs";

interface TableOfContentsProps {
  headings: DocHeading[];
}

export function TableOfContents({ headings }: TableOfContentsProps) {
  const [activeId, setActiveId] = useState<string>("");

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            setActiveId(entry.target.id);
          }
        }
      },
      { rootMargin: "-80px 0px -80% 0px" }
    );

    for (const heading of headings) {
      const el = document.getElementById(heading.id);
      if (el) observer.observe(el);
    }

    return () => observer.disconnect();
  }, [headings]);

  if (headings.length === 0) return null;

  return (
    <nav className="space-y-1">
      <h4 className="mb-3 text-sm font-semibold">On This Page</h4>
      {headings.map((heading) => (
        <a
          key={heading.id}
          href={`#${heading.id}`}
          className={cn(
            "block text-sm transition-colors hover:text-foreground",
            heading.depth === 3 && "pl-4",
            heading.depth === 4 && "pl-8",
            activeId === heading.id
              ? "font-medium text-primary"
              : "text-muted-foreground"
          )}
        >
          {heading.text}
        </a>
      ))}
    </nav>
  );
}
```

- [ ] **Step 4: Create docs layout**

Write `website/src/app/docs/layout.tsx`:

```tsx
import { getDocsSidebar } from "@/lib/docs";
import { DocsSidebar } from "@/components/docs/docs-sidebar";

export default function DocsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const sidebar = getDocsSidebar();

  return (
    <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
      <div className="flex gap-8 py-8">
        {/* Sidebar — hidden on mobile, shown on lg+ */}
        <aside className="hidden w-64 shrink-0 lg:block">
          <div className="sticky top-20">
            <DocsSidebar sections={sidebar} />
          </div>
        </aside>

        {/* Main content */}
        <div className="min-w-0 flex-1">{children}</div>
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Commit**

```bash
git add website/src/components/docs/ website/src/app/docs/layout.tsx
git commit -m "feat(website): add docs layout with sidebar, TOC, and MDX components"
```

---

### Task 8: Docs Index + Dynamic Slug Pages

**Files:**
- Create: `website/src/app/docs/page.tsx`
- Create: `website/src/app/docs/[slug]/page.tsx`

- [ ] **Step 1: Create docs index page**

Write `website/src/app/docs/page.tsx`:

```tsx
import Link from "next/link";
import { getAllDocs } from "@/lib/docs";
import { buildMetadata } from "@/lib/metadata";
import type { Metadata } from "next";

export const metadata: Metadata = buildMetadata({
  title: "Documentation",
  description:
    "Comprehensive documentation for Courier MFT — architecture, job engine, connections, encryption, API reference, and deployment guides.",
  keywords: [
    "Courier MFT documentation",
    "managed file transfer docs",
    "MFT setup guide",
  ],
  path: "/docs",
});

export default function DocsIndexPage() {
  const docs = getAllDocs();

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight">Documentation</h1>
      <p className="mt-4 text-lg text-muted-foreground">
        Everything you need to get started with Courier MFT, understand its
        architecture, and deploy it in production.
      </p>
      <div className="mt-8 grid gap-4 sm:grid-cols-2">
        {docs.map((doc) => (
          <Link
            key={doc.slug}
            href={`/docs/${doc.slug}`}
            className="group rounded-lg border bg-card p-5 transition-colors hover:border-primary/50"
          >
            <h2 className="font-semibold group-hover:text-primary">
              {doc.title}
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">
              {doc.description}
            </p>
          </Link>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Create dynamic doc page**

Write `website/src/app/docs/[slug]/page.tsx`:

```tsx
import { notFound } from "next/navigation";
import { MDXRemote } from "next-mdx-remote/rsc";
import remarkGfm from "remark-gfm";
import rehypeSlug from "rehype-slug";
import rehypeHighlight from "rehype-highlight";
import { getAllDocs, getDocBySlug, getDocSlugs } from "@/lib/docs";
import { buildDocMetadata } from "@/lib/metadata";
import { mdxComponents } from "@/components/docs/mdx-components";
import { TableOfContents } from "@/components/docs/table-of-contents";
import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import type { Metadata } from "next";

interface DocPageProps {
  params: Promise<{ slug: string }>;
}

export async function generateStaticParams() {
  return getDocSlugs().map((slug) => ({ slug }));
}

export async function generateMetadata({
  params,
}: DocPageProps): Promise<Metadata> {
  const { slug } = await params;
  const doc = getDocBySlug(slug);
  if (!doc) return {};

  return buildDocMetadata(slug, {
    title: doc.title,
    description: doc.description,
    keywords: doc.keywords,
  });
}

export default async function DocPage({ params }: DocPageProps) {
  const { slug } = await params;
  const doc = getDocBySlug(slug);

  if (!doc) {
    notFound();
  }

  // Get prev/next for navigation
  const allDocs = getAllDocs();
  const currentIndex = allDocs.findIndex((d) => d.slug === slug);
  const prev = currentIndex > 0 ? allDocs[currentIndex - 1] : null;
  const next =
    currentIndex < allDocs.length - 1 ? allDocs[currentIndex + 1] : null;

  return (
    <div className="flex gap-8">
      {/* Doc content */}
      <article className="min-w-0 flex-1">
        <h1 className="text-3xl font-bold tracking-tight">{doc.title}</h1>
        <p className="mt-2 text-lg text-muted-foreground">{doc.description}</p>

        <div className="mt-8">
          <MDXRemote
            source={doc.content}
            components={mdxComponents}
            options={{
              mdxOptions: {
                remarkPlugins: [remarkGfm],
                rehypePlugins: [rehypeSlug, rehypeHighlight],
              },
            }}
          />
        </div>

        {/* Prev / Next navigation */}
        <nav className="mt-12 flex items-center justify-between border-t pt-6">
          {prev ? (
            <Link
              href={`/docs/${prev.slug}`}
              className="group flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
            >
              <ChevronLeft className="h-4 w-4" />
              <div>
                <div className="text-xs text-muted-foreground">Previous</div>
                <div className="font-medium group-hover:text-primary">
                  {prev.title}
                </div>
              </div>
            </Link>
          ) : (
            <div />
          )}
          {next ? (
            <Link
              href={`/docs/${next.slug}`}
              className="group flex items-center gap-2 text-right text-sm text-muted-foreground hover:text-foreground"
            >
              <div>
                <div className="text-xs text-muted-foreground">Next</div>
                <div className="font-medium group-hover:text-primary">
                  {next.title}
                </div>
              </div>
              <ChevronRight className="h-4 w-4" />
            </Link>
          ) : (
            <div />
          )}
        </nav>
      </article>

      {/* Table of contents — hidden on smaller screens */}
      <aside className="hidden w-56 shrink-0 xl:block">
        <div className="sticky top-20">
          <TableOfContents headings={doc.headings} />
        </div>
      </aside>
    </div>
  );
}
```

- [ ] **Step 3: Verify docs render**

Make sure sync scripts have been run, then:
```bash
cd website && npm run dev
```

Open http://localhost:3000/docs. Verify:
- Docs index shows grid of all doc pages with titles and descriptions
- Click a doc page (e.g., "Job Engine") — renders markdown with styled headings, code blocks, tables
- Left sidebar shows all doc pages, highlights current page
- Right-side table of contents shows headings from current page
- Prev/Next links at bottom of each doc page work

- [ ] **Step 4: Commit**

```bash
git add website/src/app/docs/
git commit -m "feat(website): add docs index and dynamic doc pages with MDX rendering"
```

---

## Phase 5: Screenshots Page

### Task 9: Screenshots Gallery

**Files:**
- Create: `website/src/lib/screenshots.ts`
- Create: `website/src/app/screenshots/page.tsx`

- [ ] **Step 1: Create screenshot manifest**

Write `website/src/lib/screenshots.ts`:

```typescript
export interface Screenshot {
  src: string;
  alt: string;
  caption: string;
  category: ScreenshotCategory;
}

export type ScreenshotCategory =
  | "Dashboard"
  | "Jobs"
  | "Connections"
  | "Keys"
  | "Monitoring"
  | "Admin"
  | "Chains"
  | "Notifications";

export const screenshots: Screenshot[] = [
  // Dashboard
  {
    src: "/screenshots/dashboard.png",
    alt: "Courier MFT main dashboard with job execution overview and system health",
    caption: "Dashboard Overview",
    category: "Dashboard",
  },
  {
    src: "/screenshots/login-page.png",
    alt: "Courier MFT login page with Entra ID authentication",
    caption: "Login Page",
    category: "Dashboard",
  },
  {
    src: "/screenshots/sidebar-collapsed.png",
    alt: "Collapsed sidebar navigation showing icon-only mode",
    caption: "Collapsed Sidebar",
    category: "Dashboard",
  },
  {
    src: "/screenshots/user-menu.png",
    alt: "User menu dropdown with account settings and logout",
    caption: "User Menu",
    category: "Dashboard",
  },

  // Jobs
  {
    src: "/screenshots/jobs-list.png",
    alt: "Jobs list view showing all configured file transfer jobs with status",
    caption: "Jobs List",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-create.png",
    alt: "Create new job form with name, description, and configuration options",
    caption: "Create Job",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-detail.png",
    alt: "Job detail view showing configuration, steps, schedules, and execution history",
    caption: "Job Details",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-steps.png",
    alt: "Job pipeline builder showing multi-step configuration with SFTP, PGP, and file operations",
    caption: "Job Steps Pipeline",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-executions.png",
    alt: "Job execution history showing past runs with status, duration, and step results",
    caption: "Execution History",
    category: "Jobs",
  },
  {
    src: "/screenshots/job-schedules.png",
    alt: "Job scheduling interface with cron expression configuration",
    caption: "Job Schedules",
    category: "Jobs",
  },

  // Connections
  {
    src: "/screenshots/connections-list.png",
    alt: "Connections list showing SFTP, FTP, and local filesystem connections",
    caption: "Connections List",
    category: "Connections",
  },
  {
    src: "/screenshots/connection-create.png",
    alt: "Create connection form with protocol selection and credential configuration",
    caption: "Create Connection",
    category: "Connections",
  },
  {
    src: "/screenshots/connection-detail.png",
    alt: "Connection detail view showing configuration and test results",
    caption: "Connection Details",
    category: "Connections",
  },

  // Keys
  {
    src: "/screenshots/keys-pgp.png",
    alt: "PGP key management interface showing imported and generated keys",
    caption: "PGP Keys",
    category: "Keys",
  },
  {
    src: "/screenshots/keys-ssh.png",
    alt: "SSH key management interface for SFTP connection authentication",
    caption: "SSH Keys",
    category: "Keys",
  },
  {
    src: "/screenshots/pgp-key-detail.png",
    alt: "PGP key detail view with fingerprint, expiry, and usage information",
    caption: "PGP Key Details",
    category: "Keys",
  },
  {
    src: "/screenshots/pgp-key-generate.png",
    alt: "PGP key generation wizard with algorithm and key size options",
    caption: "Generate PGP Key",
    category: "Keys",
  },
  {
    src: "/screenshots/ssh-key-generate.png",
    alt: "SSH key generation wizard with key type and size options",
    caption: "Generate SSH Key",
    category: "Keys",
  },

  // Monitoring
  {
    src: "/screenshots/monitors-list.png",
    alt: "File monitors list showing directory watch configurations and status",
    caption: "Monitors List",
    category: "Monitoring",
  },
  {
    src: "/screenshots/monitor-create.png",
    alt: "Create file monitor form with directory path and trigger configuration",
    caption: "Create Monitor",
    category: "Monitoring",
  },
  {
    src: "/screenshots/monitor-detail.png",
    alt: "Monitor detail view showing watch configuration and triggered events",
    caption: "Monitor Details",
    category: "Monitoring",
  },

  // Admin
  {
    src: "/screenshots/admin-users.png",
    alt: "User management interface with role assignments and account status",
    caption: "User Management",
    category: "Admin",
  },
  {
    src: "/screenshots/admin-settings.png",
    alt: "System settings configuration panel",
    caption: "System Settings",
    category: "Admin",
  },
  {
    src: "/screenshots/audit-log.png",
    alt: "Audit log showing timestamped records of all user actions and system events",
    caption: "Audit Log",
    category: "Admin",
  },
  {
    src: "/screenshots/my-account.png",
    alt: "User account page with profile settings and password change",
    caption: "My Account",
    category: "Admin",
  },
  {
    src: "/screenshots/tags-page.png",
    alt: "Tag management for organizing jobs, connections, and other entities",
    caption: "Tags",
    category: "Admin",
  },

  // Chains
  {
    src: "/screenshots/chains-list.png",
    alt: "Job chains list showing linked job sequences for complex workflows",
    caption: "Chains List",
    category: "Chains",
  },
  {
    src: "/screenshots/chain-create.png",
    alt: "Create job chain form for linking multiple jobs in sequence",
    caption: "Create Chain",
    category: "Chains",
  },
  {
    src: "/screenshots/chain-detail.png",
    alt: "Chain detail view showing linked job sequence and execution flow",
    caption: "Chain Details",
    category: "Chains",
  },

  // Notifications
  {
    src: "/screenshots/notifications-list.png",
    alt: "Notification configurations for job success and failure alerts",
    caption: "Notifications List",
    category: "Notifications",
  },
  {
    src: "/screenshots/notification-create.png",
    alt: "Create notification rule with event type and delivery method",
    caption: "Create Notification",
    category: "Notifications",
  },
];

export const categories: ScreenshotCategory[] = [
  "Dashboard",
  "Jobs",
  "Connections",
  "Keys",
  "Monitoring",
  "Admin",
  "Chains",
  "Notifications",
];
```

- [ ] **Step 2: Create screenshots page**

Write `website/src/app/screenshots/page.tsx`:

```tsx
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
```

- [ ] **Step 3: Add screenshots page metadata**

Since the page is a client component, we need a separate metadata export. Create `website/src/app/screenshots/layout.tsx`:

```tsx
import type { Metadata } from "next";
import { buildMetadata } from "@/lib/metadata";

export const metadata: Metadata = buildMetadata({
  title: "Screenshots",
  description:
    "See Courier MFT in action — dashboard, job builder, connections, encryption keys, monitoring, audit logs, and more.",
  keywords: [
    "MFT dashboard",
    "file transfer UI",
    "managed file transfer screenshots",
    "Courier MFT interface",
  ],
  path: "/screenshots",
});

export default function ScreenshotsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <>{children}</>;
}
```

- [ ] **Step 4: Verify screenshots page**

```bash
cd website && npm run dev
```

Open http://localhost:3000/screenshots. Verify:
- Category filter buttons work (All, Dashboard, Jobs, etc.)
- Screenshots display in grid with captions
- Clicking a screenshot opens lightbox dialog
- Images load correctly from `/screenshots/` path

- [ ] **Step 5: Commit**

```bash
git add website/src/lib/screenshots.ts website/src/app/screenshots/
git commit -m "feat(website): add screenshots gallery with category filter and lightbox"
```

---

## Phase 6: SEO

### Task 10: JSON-LD Structured Data + Sitemap

**Files:**
- Create: `website/src/components/seo/json-ld.tsx`
- Create: `website/next-sitemap.config.js`
- Modify: `website/src/app/layout.tsx`
- Modify: `website/package.json`

- [ ] **Step 1: Create JSON-LD component**

Write `website/src/components/seo/json-ld.tsx`:

```tsx
interface JsonLdProps {
  data: Record<string, unknown>;
}

export function JsonLd({ data }: JsonLdProps) {
  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: JSON.stringify(data) }}
    />
  );
}

export const softwareApplicationLd = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "Courier MFT",
  applicationCategory: "BusinessApplication",
  operatingSystem: "Cross-platform",
  description:
    "Open source managed file transfer platform. Replace SFTP scripts, PGP workflows, and cron jobs with a single auditable platform.",
  offers: {
    "@type": "Offer",
    price: "0",
    priceCurrency: "USD",
  },
  license: "https://www.apache.org/licenses/LICENSE-2.0",
  url: "https://couriermft.com",
  downloadUrl: "https://github.com/Battle-Line-Productions/Courier",
  author: {
    "@type": "Organization",
    name: "Battle Line Productions",
    url: "https://github.com/Battle-Line-Productions",
  },
};

export const websiteLd = {
  "@context": "https://schema.org",
  "@type": "WebSite",
  name: "Courier MFT",
  url: "https://couriermft.com",
  potentialAction: {
    "@type": "SearchAction",
    target: {
      "@type": "EntryPoint",
      urlTemplate: "https://couriermft.com/docs?q={search_term_string}",
    },
    "query-input": "required name=search_term_string",
  },
};

export function buildBreadcrumbLd(
  items: { name: string; href: string }[]
): Record<string, unknown> {
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: items.map((item, i) => ({
      "@type": "ListItem",
      position: i + 1,
      name: item.name,
      item: `https://couriermft.com${item.href}`,
    })),
  };
}
```

- [ ] **Step 2: Add JSON-LD to homepage**

Update `website/src/app/page.tsx` — add structured data at the top of the component:

```tsx
import { Hero } from "@/components/home/hero";
import { Features } from "@/components/home/features";
import { QuickStart } from "@/components/home/quick-start";
import { ScreenshotShowcase } from "@/components/home/screenshot-showcase";
import { TechStack } from "@/components/home/tech-stack";
import { JsonLd, softwareApplicationLd, websiteLd } from "@/components/seo/json-ld";

export default function HomePage() {
  return (
    <>
      <JsonLd data={softwareApplicationLd} />
      <JsonLd data={websiteLd} />
      <Hero />
      <Features />
      <ScreenshotShowcase />
      <QuickStart />
      <TechStack />
    </>
  );
}
```

- [ ] **Step 3: Create next-sitemap config**

Write `website/next-sitemap.config.js`:

```javascript
/** @type {import('next-sitemap').IConfig} */
module.exports = {
  siteUrl: "https://couriermft.com",
  generateRobotsTxt: true,
  outDir: "./out",
  robotsTxtOptions: {
    policies: [
      {
        userAgent: "*",
        allow: "/",
      },
    ],
  },
};
```

- [ ] **Step 4: Add postbuild script to package.json**

Update the `scripts` section in `website/package.json`:

```json
"scripts": {
  "dev": "next dev",
  "prebuild": "npx tsx scripts/sync-docs.ts && npx tsx scripts/sync-screenshots.ts",
  "build": "next build",
  "postbuild": "next-sitemap",
  "start": "next start",
  "lint": "next lint"
}
```

- [ ] **Step 5: Verify static build produces sitemap**

```bash
cd website && npm run build
```

Expected: Build succeeds. Check output:
```bash
ls website/out/sitemap*.xml website/out/robots.txt
```

Expected: `sitemap-0.xml`, `sitemap.xml` (index), and `robots.txt` all present.

- [ ] **Step 6: Commit**

```bash
git add website/src/components/seo/ website/src/app/page.tsx website/next-sitemap.config.js website/package.json
git commit -m "feat(website): add JSON-LD structured data, sitemap, and robots.txt generation"
```

---

## Phase 7: Client-Side Search

### Task 11: Build-Time Search Index + Search UI

**Files:**
- Create: `website/src/lib/search.ts`
- Create: `website/src/components/docs/search-dialog.tsx`
- Modify: `website/src/components/layout/header.tsx`

- [ ] **Step 1: Create search library**

Write `website/src/lib/search.ts`:

```typescript
import type { DocPage } from "./docs";

export interface SearchResult {
  slug: string;
  title: string;
  description: string;
  matchedContent: string;
}

/**
 * Simple client-side search using string matching.
 * For the initial implementation, we use a straightforward approach.
 * Can be upgraded to flexsearch later if needed.
 */
export function searchDocs(
  docs: Array<Pick<DocPage, "slug" | "title" | "description" | "content">>,
  query: string
): SearchResult[] {
  if (!query.trim()) return [];

  const terms = query.toLowerCase().split(/\s+/);

  const results: Array<SearchResult & { score: number }> = [];

  for (const doc of docs) {
    const titleLower = doc.title.toLowerCase();
    const descLower = doc.description.toLowerCase();
    const contentLower = doc.content.toLowerCase();

    let score = 0;

    for (const term of terms) {
      if (titleLower.includes(term)) score += 10;
      if (descLower.includes(term)) score += 5;
      if (contentLower.includes(term)) score += 1;
    }

    if (score === 0) continue;

    // Extract a content snippet around the first match
    let matchedContent = doc.description;
    const contentIndex = contentLower.indexOf(terms[0]);
    if (contentIndex !== -1) {
      const start = Math.max(0, contentIndex - 40);
      const end = Math.min(doc.content.length, contentIndex + 120);
      matchedContent =
        (start > 0 ? "..." : "") +
        doc.content.slice(start, end).replace(/\n/g, " ").trim() +
        (end < doc.content.length ? "..." : "");
    }

    results.push({
      slug: doc.slug,
      title: doc.title,
      description: doc.description,
      matchedContent,
      score,
    });
  }

  return results.sort((a, b) => b.score - a.score).slice(0, 10);
}
```

- [ ] **Step 2: Create SearchDialog component**

Write `website/src/components/docs/search-dialog.tsx`:

```tsx
"use client";

import { useState, useEffect, useCallback } from "react";
import { useRouter } from "next/navigation";
import { Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
} from "@/components/ui/dialog";
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

  // Ctrl+K / Cmd+K shortcut
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
```

- [ ] **Step 3: Wire search into header**

Update `website/src/components/layout/header.tsx` — add the search dialog. Import `getAllDocs` and pass docs data to the search component.

Since the Header is a client component and we can't call `getAllDocs()` (server-side) directly, we need to restructure slightly. Create a wrapper that fetches docs server-side and passes them down.

Create `website/src/components/layout/header-with-search.tsx`:

```tsx
import { getAllDocs } from "@/lib/docs";
import { Header } from "./header";

export function HeaderWithSearch() {
  const docs = getAllDocs().map((d) => ({
    slug: d.slug,
    title: d.title,
    description: d.description,
    content: d.content,
  }));

  return <Header searchDocs={docs} />;
}
```

Then update `website/src/components/layout/header.tsx` to accept `searchDocs` prop:

Add at the top of the file, after other imports:
```tsx
import { SearchDialog } from "@/components/docs/search-dialog";
import type { DocPage } from "@/lib/docs";
```

Update the component signature:
```tsx
interface HeaderProps {
  searchDocs?: Array<Pick<DocPage, "slug" | "title" | "description" | "content">>;
}

export function Header({ searchDocs }: HeaderProps) {
```

Add the SearchDialog inside the header, right before the ThemeToggle:
```tsx
{searchDocs && <SearchDialog docs={searchDocs} />}
<ThemeToggle />
```

Finally, update `website/src/app/layout.tsx` to use `HeaderWithSearch` instead of `Header`:

```tsx
import { HeaderWithSearch } from "@/components/layout/header-with-search";
```

Replace `<Header />` with `<HeaderWithSearch />`.

- [ ] **Step 4: Verify search works**

```bash
cd website && npm run dev
```

Open http://localhost:3000. Verify:
- "Search docs..." button appears in header
- Pressing Ctrl+K opens search dialog
- Typing "encryption" shows relevant doc results
- Clicking a result navigates to that doc page

- [ ] **Step 5: Commit**

```bash
git add website/src/lib/search.ts website/src/components/docs/search-dialog.tsx website/src/components/layout/header-with-search.tsx website/src/components/layout/header.tsx website/src/app/layout.tsx
git commit -m "feat(website): add client-side doc search with Ctrl+K shortcut"
```

---

## Phase 8: Cloudflare Pages + Final Polish

### Task 12: Cloudflare Pages Configuration

**Files:**
- Create: `website/_headers`
- Create: `website/_redirects`
- Create: `website/public/favicon.ico` (placeholder — user should replace)

- [ ] **Step 1: Create Cloudflare headers file**

Write `website/public/_headers`:

```
/*
  X-Frame-Options: DENY
  X-Content-Type-Options: nosniff
  Referrer-Policy: strict-origin-when-cross-origin
  Permissions-Policy: camera=(), microphone=(), geolocation=()

/_next/static/*
  Cache-Control: public, max-age=31536000, immutable

/screenshots/*
  Cache-Control: public, max-age=86400
```

- [ ] **Step 2: Create Cloudflare redirects file**

Write `website/public/_redirects`:

```
https://www.couriermft.com/* https://couriermft.com/:splat 301
```

- [ ] **Step 3: Verify full static build**

```bash
cd website && npm run build
```

Expected: Build succeeds with no errors. Verify output:
```bash
ls website/out/
```

Expected: `index.html`, `docs/` directory with pages, `screenshots/` directory, `sitemap.xml`, `robots.txt`, `_headers`, `_redirects`.

- [ ] **Step 4: Commit**

```bash
git add website/public/_headers website/public/_redirects
git commit -m "feat(website): add Cloudflare Pages headers and redirects"
```

---

### Task 13: Final Verification + Build Smoke Test

- [ ] **Step 1: Clean build from scratch**

```bash
cd website
rm -rf node_modules out docs-content .next
npm install
npm run build
```

Expected: Full build succeeds — sync scripts run, docs split, screenshots copied, Next.js builds, sitemap generated. `website/out/` contains the complete static site.

- [ ] **Step 2: Serve and verify locally**

```bash
cd website && npx serve out
```

Open the URL shown. Verify:
- Homepage renders all sections
- Dark mode toggle works
- Nav links work (Docs, Screenshots)
- /docs shows index, clicking pages renders markdown
- /docs/[slug] pages have sidebar, TOC, prev/next nav
- /screenshots shows gallery with filter and lightbox
- Search dialog opens with Ctrl+K and returns results
- View source shows JSON-LD in `<head>`
- No console errors

- [ ] **Step 3: Commit any final fixes**

If any issues were found and fixed:
```bash
git add -A website/
git commit -m "fix(website): address build verification issues"
```
