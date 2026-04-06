import type { Metadata } from "next";
import { ThemeProvider } from "@/components/layout/theme-provider";
import { HeaderWithSearch } from "@/components/layout/header-with-search";
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
            <HeaderWithSearch />
            <main className="flex-1">{children}</main>
            <Footer />
          </div>
        </ThemeProvider>
      </body>
    </html>
  );
}
