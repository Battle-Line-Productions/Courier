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
  "1. Executive Summary": {
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
  "2. Architecture Overview": {
    slug: "architecture",
    description:
      "System architecture, deployment units, and dependency layers for Courier MFT.",
    keywords: [
      "MFT architecture",
      "file transfer platform design",
      "vertical slice architecture",
    ],
  },
  "3. Tech Stack & Key Libraries": {
    slug: "tech-stack",
    description:
      "Technologies and libraries used in Courier MFT — .NET, PostgreSQL, Next.js, and more.",
    keywords: ["MFT tech stack", ".NET file transfer", "PostgreSQL MFT"],
  },
  "4. Domain Model": {
    slug: "domain-model",
    description:
      "Entities, value objects, enums, and relationships in Courier's domain model.",
    keywords: [
      "MFT domain model",
      "file transfer entities",
      "job scheduling model",
    ],
  },
  "5. Job Engine Design": {
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
  "6. Connection & Protocol Layer": {
    slug: "connections",
    description:
      "Manage SFTP, FTP, local filesystem, and Azure Function connections securely.",
    keywords: [
      "SFTP connection management",
      "FTP automation",
      "secure file transfer connections",
    ],
  },
  "7. Cryptography & Key Store": {
    slug: "encryption",
    description:
      "AES-256-GCM envelope encryption, PGP key management, and Azure Key Vault integration.",
    keywords: [
      "PGP encryption automation",
      "AES-256 file transfer",
      "key management MFT",
    ],
  },
  "8. File Operations": {
    slug: "file-operations",
    description:
      "File copy, move, compression, and transformation operations within job pipelines.",
    keywords: ["file compression automation", "file operations pipeline"],
  },
  "9. File Monitor System": {
    slug: "monitors",
    description:
      "Watch directories and trigger jobs automatically when files arrive.",
    keywords: [
      "file monitor system",
      "directory watch triggers",
      "automated file detection",
    ],
  },
  "10. API Design": {
    slug: "api",
    description:
      "RESTful API for managing jobs, connections, keys, and system configuration.",
    keywords: [
      "file transfer REST API",
      "MFT API",
      "managed file transfer API",
    ],
  },
  "11. Frontend Architecture": {
    slug: "frontend",
    description:
      "Next.js dashboard for managing all file transfer operations through a modern UI.",
    keywords: ["MFT dashboard", "file transfer UI", "management console"],
  },
  "12. Security": {
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
  "13. Database Schema": {
    slug: "database",
    description:
      "PostgreSQL schema design — tables, indexes, and migration strategy.",
    keywords: ["MFT database schema", "file transfer database design"],
  },
  "14. Deployment & Infrastructure": {
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

/**
 * Escape curly braces in markdown content so MDX doesn't interpret them
 * as JSX expressions. Skips code blocks (``` ... ```) where braces are expected.
 */
function escapeBracesForMdx(content: string): string {
  const codeBlockRegex = /```[\s\S]*?```/g;
  const parts: { text: string; isCode: boolean }[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = codeBlockRegex.exec(content)) !== null) {
    if (match.index > lastIndex) {
      parts.push({ text: content.slice(lastIndex, match.index), isCode: false });
    }
    parts.push({ text: match[0], isCode: true });
    lastIndex = match.index + match[0].length;
  }
  if (lastIndex < content.length) {
    parts.push({ text: content.slice(lastIndex), isCode: false });
  }

  return parts
    .map((part) => {
      if (part.isCode) return part.text;
      // Escape { and } outside code blocks
      return part.text.replace(/\{/g, "\\{").replace(/\}/g, "\\}");
    })
    .join("");
}

function splitDesignDoc(content: string): DocPage[] {
  const pages: DocPage[] = [];
  // Match H2 headers like "## 1. Executive Summary"
  const sectionRegex = /^## (\d+\.\s+.+)$/gm;
  const matches: { title: string; index: number }[] = [];

  let match: RegExpExecArray | null;
  while ((match = sectionRegex.exec(content)) !== null) {
    matches.push({ title: match[1].trim(), index: match.index });
  }

  for (let i = 0; i < matches.length; i++) {
    const { title, index } = matches[i];
    const endIndex =
      i + 1 < matches.length ? matches[i + 1].index : content.length;
    const sectionContent = content.slice(index, endIndex).trim();

    const mapping = SECTION_MAP[title];
    if (!mapping) {
      console.warn(`  No mapping for section: "${title}" — skipping`);
      continue;
    }

    pages.push({
      slug: mapping.slug,
      title: title.replace(/^\d+\.\s+/, ""), // Remove "1. " prefix for display
      description: mapping.description,
      order: pages.length + 2, // 1 is reserved for getting-started
      keywords: mapping.keywords,
      // Remove the H2 header line, escape braces for MDX
      content: escapeBracesForMdx(sectionContent.replace(/^## .+\n+/, "")),
    });
  }

  return pages;
}

function buildGettingStarted(readmeContent: string): DocPage {
  // Extract from "## Quick Start" through the next major section
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
    content: escapeBracesForMdx(content),
  };
}

function buildContributing(contributingContent: string): DocPage {
  return {
    slug: "contributing",
    title: "Contributing",
    description:
      "How to contribute to Courier MFT — bug reports, feature requests, and pull requests.",
    order: 99,
    keywords: ["contribute open source MFT", "courier contributing guide"],
    content: escapeBracesForMdx(contributingContent),
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
