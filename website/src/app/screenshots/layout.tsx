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
