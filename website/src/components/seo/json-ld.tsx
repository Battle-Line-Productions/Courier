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
