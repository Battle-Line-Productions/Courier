"use client";

import { GuideImage } from "@/components/guide/guide-image";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function TagsGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Tags</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Tags let you organize and categorize your resources. Assign colored labels to
          jobs, connections, and other entities to make them easier to find and filter.
        </p>
      </div>

      {/* Tags Page */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Managing Tags</h2>
        <p className="text-sm text-muted-foreground">
          The Tags page shows all tags in the system with their name, color, category,
          and how many entities they&apos;re assigned to.
        </p>
        <GuideImage
          src="/guide/screenshots/tags-page.png"
          alt="Tags page"
          caption="Manage all tags with their colors and categories"
        />
      </section>

      {/* Creating Tags */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Creating Tags</h2>
        <p className="text-sm text-muted-foreground">
          Click <strong>+ Create Tag</strong> to add a new tag. Each tag has:
        </p>
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>
            <strong>Name</strong> — A short, descriptive label (e.g., &quot;production&quot;,
            &quot;daily&quot;, &quot;high-priority&quot;)
          </li>
          <li>
            <strong>Color</strong> — A hex color that makes the tag visually distinct in
            lists and detail views
          </li>
          <li>
            <strong>Category</strong> — An optional grouping like &quot;environment&quot;,
            &quot;schedule&quot;, or &quot;team&quot; to organize related tags
          </li>
        </ul>
      </section>

      {/* Assigning Tags */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Assigning Tags</h2>
        <p className="text-sm text-muted-foreground">
          Tags can be assigned to entities from their respective detail pages. For
          example, open a job&apos;s detail page and use the tag selector to add or remove
          tags.
        </p>
        <p className="text-sm text-muted-foreground">
          Once assigned, tags appear as colored badges in list views, and you can filter
          by tag using the tag dropdown on list pages (like the Jobs list).
        </p>
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>Tip:</strong> Use a consistent tagging strategy across your team. For
            example, tag all production jobs with &quot;production&quot; and all staging
            jobs with &quot;staging&quot; to quickly filter by environment.
          </p>
        </div>
      </section>

      <GuidePrevNext
        prev={{ label: "Notifications", href: "/guide/notifications" }}
        next={{ label: "Administration", href: "/guide/admin" }}
      />
    </div>
  );
}
