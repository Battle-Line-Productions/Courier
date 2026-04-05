"use client";

import { GuideNav } from "@/components/guide/guide-nav";

export default function GuideLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex gap-8">
      <aside className="hidden w-52 shrink-0 lg:block">
        <GuideNav />
      </aside>
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}
