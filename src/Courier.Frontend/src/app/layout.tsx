import type { Metadata } from "next";
import { Providers } from "@/components/providers";
import { Shell } from "@/components/layout/shell";
import { Toaster } from "@/components/ui/sonner";
import "./globals.css";

export const metadata: Metadata = {
  title: "Courier",
  description: "Enterprise file transfer & job management",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="antialiased">
        <Providers>
          <Shell>{children}</Shell>
          <Toaster />
        </Providers>
      </body>
    </html>
  );
}
