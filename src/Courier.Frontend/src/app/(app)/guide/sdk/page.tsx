"use client";

import Link from "next/link";
import { Cloud } from "lucide-react";
import { GuidePrevNext } from "@/components/guide/guide-nav";

const sdks = [
  {
    title: "Azure Functions",
    href: "/guide/sdk/azure-functions",
    icon: Cloud,
    package: "Courier.Functions.Sdk",
    description:
      "Integrate Azure Functions with Courier job pipelines. Report success or failure back to Courier with a single class — supports both callback and fire-and-forget modes.",
  },
];

export default function SdkOverview() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Developer SDKs
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Courier provides SDKs for integrating external services into your job
          pipelines. Each SDK is a lightweight NuGet package with minimal dependencies,
          designed to make integration as simple as possible.
        </p>
      </div>

      <div className="grid gap-4">
        {sdks.map((sdk) => (
          <Link
            key={sdk.href}
            href={sdk.href}
            className="group rounded-lg border p-5 transition-colors hover:border-primary/40 hover:bg-primary/5"
          >
            <div className="flex items-center gap-3">
              <div className="flex h-9 w-9 items-center justify-center rounded-md bg-primary/10 text-primary">
                <sdk.icon className="h-5 w-5" />
              </div>
              <div>
                <h3 className="text-sm font-semibold group-hover:text-primary">
                  {sdk.title}
                </h3>
                <code className="text-xs text-muted-foreground">
                  {sdk.package}
                </code>
              </div>
            </div>
            <p className="mt-3 text-sm text-muted-foreground">
              {sdk.description}
            </p>
          </Link>
        ))}
      </div>

      <GuidePrevNext
        prev={{ label: "Administration", href: "/guide/admin" }}
      />
    </div>
  );
}
