export interface DocKeywords {
  title: string;
  description: string;
  keywords: string[];
}

/**
 * Fallback keyword map for static pages (not doc pages).
 * Doc pages get their keywords from frontmatter injected by sync-docs.
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
