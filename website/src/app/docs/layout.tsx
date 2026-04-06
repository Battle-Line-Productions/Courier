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
