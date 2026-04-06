# Courier Website Design Spec

**Date:** 2026-04-05
**Domain:** couriermft.com
**Purpose:** Open-source project website — marketing landing page + full documentation

---

## 1. Overview

A standalone Next.js website in `website/` at the repo root that serves as Courier's public-facing project site and documentation home. It combines a polished landing page with full docs rendered from the existing repo markdown. Deployed as a static site to Cloudflare Pages.

**Goals:**
- Attract developers and ops teams searching for open-source MFT solutions
- Provide comprehensive documentation without maintaining a separate doc source
- Rank well for managed file transfer, SFTP automation, and related keywords
- Present a professional, credible face for the project

**Non-goals:**
- No pricing, sign-up flows, or SaaS features — this is an open-source project site
- No backend, API, or database — pure static HTML
- No changelog page (can be added later)

---

## 2. Tech Stack

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Framework | Next.js 15 (App Router, static export) | Team expertise, Cloudflare Pages support |
| Styling | Tailwind CSS + shadcn/ui | Consistent with product frontend |
| Dark mode | next-themes (system default, toggle in header) | Expected for dev-facing sites |
| Docs rendering | next-mdx-remote | Renders repo markdown with custom components |
| Search | flexsearch (client-side, build-time index) | No backend needed, smaller than fuse.js |
| Deployment | Cloudflare Pages | DNS already on Cloudflare, free, fast CDN |
| SEO | next-sitemap, JSON-LD, Next.js Metadata API | Strong organic discovery |

---

## 3. Project Structure

```
website/
├── public/
│   ├── screenshots/          # Copied from product at build time
│   ├── favicon.ico
│   ├── og-image.png          # Default social share image
│   └── robots.txt
├── src/
│   ├── app/
│   │   ├── layout.tsx        # Root layout (nav, footer, theme provider)
│   │   ├── page.tsx          # Homepage
│   │   ├── docs/
│   │   │   ├── layout.tsx    # Docs layout (sidebar + content)
│   │   │   ├── page.tsx      # Docs index (/docs)
│   │   │   └── [slug]/
│   │   │       └── page.tsx  # Dynamic doc pages
│   │   └── screenshots/
│   │       └── page.tsx      # Screenshot gallery
│   ├── components/
│   │   ├── ui/               # shadcn/ui components
│   │   ├── layout/           # Header, Footer, Sidebar, ThemeToggle
│   │   ├── home/             # Hero, Features, QuickStart, Screenshots
│   │   ├── docs/             # DocsSidebar, TableOfContents, MDXComponents
│   │   └── seo/              # JsonLd, MetaTags
│   ├── lib/
│   │   ├── docs.ts           # Reads & parses markdown at build time
│   │   ├── screenshots.ts    # Screenshot manifest with alt text & categories
│   │   ├── metadata.ts       # SEO metadata factory
│   │   └── keywords.ts       # Keyword definitions per topic
│   └── styles/
│       └── globals.css
├── docs-content/              # Build artifact (gitignored) — split docs
├── scripts/
│   ├── sync-docs.ts          # Splits design doc into individual pages
│   └── sync-screenshots.ts   # Copies screenshots into public/
├── next.config.ts
├── tailwind.config.ts
├── package.json
└── tsconfig.json
```

---

## 4. Pages

### 4.1 Homepage (`/`)

| Section | Content | SEO Purpose |
|---------|---------|-------------|
| Hero | Tagline, subtitle, two CTAs (Get Started, GitHub) | Primary keywords in H1 |
| Feature grid | 6-8 cards: Jobs, SFTP/FTP, PGP, Scheduling, Audit, RBAC, Monitors, Azure Functions | Secondary keyword targets |
| Screenshot showcase | 3-4 key screenshots with captions | Image alt text for long-tail |
| Quick start | Code block: clone, docker compose up, open browser | "self-hosted" / "how to install" queries |
| Tech stack badges | .NET, PostgreSQL, Next.js, Docker logos | Stack-specific searches |
| Footer | Links to docs, GitHub, contributing, license | Internal linking |

### 4.2 Docs (`/docs`, `/docs/[slug]`)

Built from `Docs/CourierDesignDoc.md` split by section:

| Route | Source | Target Keywords |
|-------|--------|-----------------|
| `/docs/getting-started` | README.md quick start | `install courier`, `self-hosted MFT setup` |
| `/docs/architecture` | Design doc section 2 | `MFT architecture`, `file transfer platform design` |
| `/docs/jobs` | Design doc section 5 | `file transfer job engine`, `pipeline orchestration` |
| `/docs/connections` | Design doc section 6 | `SFTP connection management`, `FTP automation` |
| `/docs/encryption` | Design doc section 7 | `PGP encryption automation`, `AES-256 file transfer` |
| `/docs/file-operations` | Design doc section 8 | `file compression automation` |
| `/docs/monitors` | Design doc section 9 | `file monitor system`, `directory watch triggers` |
| `/docs/api` | Design doc section 10 | `file transfer REST API`, `MFT API` |
| `/docs/frontend` | Design doc section 11 | `MFT dashboard`, `file transfer UI` |
| `/docs/security` | Design doc section 12 | `secure file transfer`, `RBAC file transfer` |
| `/docs/database` | Design doc section 13 | `MFT database schema` |
| `/docs/deployment` | Design doc section 14 | `deploy MFT`, `docker file transfer` |

**Docs layout features:**
- Left sidebar with collapsible sections
- Right-side table of contents (auto-generated from headings)
- Previous/Next navigation at page bottom
- Client-side search (build-time index, no backend)

### 4.3 Screenshots (`/screenshots`)

- Filterable gallery grouped by category (Dashboard, Jobs, Connections, Keys, Monitoring, Admin)
- Descriptive alt text and captions per image
- Lightbox on click

### 4.4 Contributing (`/docs/contributing`)

- Lives as the last entry in the docs sidebar (not a standalone page)
- Rendered from `CONTRIBUTING.md`
- Links to GitHub issues, PR template

---

## 5. Docs Build Pipeline (Single Source of Truth)

### 5.1 sync-docs.ts

Runs at build time. Splits `Docs/CourierDesignDoc.md` by H1/H2 headers into individual files in `docs-content/`. Injects frontmatter:

```markdown
---
title: "Job Engine"
description: "Build multi-step file transfer pipelines with sequential step execution"
order: 5
keywords: ["job engine", "file transfer pipeline", "step orchestration"]
---

(original section content)
```

Also copies `CONTRIBUTING.md` and extracts getting-started content from `README.md`.

### 5.2 sync-screenshots.ts

Copies from `src/Courier.Frontend/public/guide/screenshots/` into `website/public/screenshots/`.

### 5.3 lib/docs.ts

Reads `docs-content/` at build time, parses frontmatter, extracts headings for table of contents. Used by `generateStaticParams()` and page components.

### 5.4 docs-content/

Build artifact. Gitignored. Never edited directly.

### 5.5 Update workflow

1. Edit `Docs/CourierDesignDoc.md` or `CONTRIBUTING.md` as normal
2. Push to main
3. Cloudflare Pages rebuilds — sync scripts re-split, site updates

---

## 6. SEO Implementation

### 6.1 Keyword Strategy

**Primary:** `managed file transfer`, `open source MFT`, `enterprise file transfer`

**Secondary:** `SFTP automation`, `PGP encryption tool`, `file transfer orchestration`, `secure file transfer platform`

**Long-tail:** `open source SFTP job scheduler`, `automate PGP encrypt and transfer`, `self-hosted file transfer`, `MFT alternative to GoAnywhere`, `free managed file transfer software`

### 6.2 Per-Page Metadata

Every page uses Next.js `generateMetadata()` with:
- Unique `<title>` (format: `Page Title | Courier MFT`)
- Unique `<meta description>` (keyword-rich, 150-160 chars)
- `keywords` meta tag
- Canonical URL
- Open Graph: title, description, image, url
- Twitter Card: summary_large_image

`metadata.ts` factory + `keywords.ts` map automate this per doc slug.

### 6.3 Structured Data (JSON-LD)

**Homepage:**
- `SoftwareApplication` (name, category, OS, price: 0, license)
- `WebSite` with `SearchAction` for sitelinks
- `Organization` for Battle Line Productions

**Doc pages:**
- `BreadcrumbList` for navigation breadcrumbs

### 6.4 Technical SEO

- `sitemap.xml` auto-generated via `next-sitemap`
- `robots.txt` — allow all, point to sitemap
- `<html lang="en">`
- Proper H1/H2/H3 hierarchy (one H1 per page)
- All images have descriptive `alt` text
- Internal linking between related doc pages
- `www.couriermft.com` → `couriermft.com` redirect

---

## 7. Theme & Dark Mode

**Light (default):**
- White background, near-black text
- Blue primary accent (Tailwind `blue-500`)
- Light gray card/section backgrounds
- Light code blocks with syntax highlighting

**Dark:**
- Near-black background (`#0a0a0a`), light gray text
- Same blue accent, slightly brighter
- Dark card backgrounds (`#111` / `#161616`)
- Dark code blocks

**Implementation:**
- `next-themes` with `attribute="class"` (Tailwind `dark:` prefix)
- Sun/moon toggle in header
- Respects system preference on first visit (`defaultTheme="system"`)
- No FOUC — next-themes handles inline script

**Typography:**
- System font stack for body (or Inter)
- Monospace for code blocks (system monospace)
- `@tailwindcss/typography` (`prose`) for markdown rendering

---

## 8. Cloudflare Pages Deployment

**Build configuration:**
- Build command: `cd website && npm run build`
- Output directory: `website/out`
- Every push to `main` → production deploy
- PRs → preview deploy at `<hash>.couriermft.pages.dev`

**npm run build internally:**
1. `sync-docs.ts` — split design doc into pages
2. `sync-screenshots.ts` — copy screenshots
3. `next build` — generate static HTML

**Domain:**
- Custom domain: `couriermft.com` + `www.couriermft.com` redirect
- DNS already on Cloudflare — one-click hookup

**Headers:**
- Aggressive caching for static assets
- Security headers: CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy

**No server, functions, workers, Docker, or CI config files needed.**

---

## 9. Dependencies (anticipated)

```json
{
  "next": "^15.x",
  "react": "^19.x",
  "tailwindcss": "^4.x",
  "next-themes": "^0.4.x",
  "next-mdx-remote": "^5.x",
  "next-sitemap": "^4.x",
  "gray-matter": "^4.x",
  "flexsearch": "^0.7.x",
  "lucide-react": "latest",
  "rehype-highlight": "latest",
  "rehype-slug": "latest",
  "remark-gfm": "latest"
}
```

Plus shadcn/ui components added via CLI.
