"use client";

import { GuideImage } from "@/components/guide/guide-image";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function MonitorsGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Monitors</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Monitors watch directories for new files and automatically trigger jobs when
          matching files appear. This is ideal for scenarios where you need to process
          incoming files as soon as they arrive.
        </p>
      </div>

      {/* Monitors List */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Viewing Monitors</h2>
        <p className="text-sm text-muted-foreground">
          The Monitors page lists all configured file watchers with their name, watch
          target, polling interval, and current status.
        </p>
        <GuideImage
          src="/guide/screenshots/monitors-list.png"
          alt="Monitors list page"
          caption="All configured file monitors with their targets and status"
        />
      </section>

      {/* Creating a Monitor */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Creating a Monitor</h2>
        <p className="text-sm text-muted-foreground">
          Click <strong>+ Create Monitor</strong> and configure the following settings:
        </p>
        <GuideImage
          src="/guide/screenshots/monitor-create.png"
          alt="Create Monitor form"
          caption="Configure a new file monitor with watch target and trigger settings"
        />
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>
            <strong>Name</strong> — A descriptive label for the monitor
          </li>
          <li>
            <strong>Watch Target</strong> — The directory path to monitor for new files.
            Specify the type (local filesystem) and the path.
          </li>
          <li>
            <strong>Trigger Events</strong> — What file events trigger the monitor (e.g.,
            file created)
          </li>
          <li>
            <strong>Polling Interval</strong> — How frequently (in seconds) Courier checks
            the directory for changes
          </li>
          <li>
            <strong>Trigger Jobs</strong> — Which jobs to automatically run when a matching
            file is detected
          </li>
        </ul>
      </section>

      {/* Monitor Detail */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Monitor Details</h2>
        <p className="text-sm text-muted-foreground">
          Click a monitor to view its detail page, where you can see its configuration,
          status, and recent activity. You can also enable/disable the monitor or
          acknowledge errors.
        </p>
        <GuideImage
          src="/guide/screenshots/monitor-detail.png"
          alt="Monitor detail page"
          caption="Monitor detail with status, configuration, and activity history"
        />
      </section>

      {/* Best Practices */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Best Practices</h2>
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>
            Set polling intervals appropriate to your file arrival patterns — 60 seconds
            is a good default, but high-frequency directories may need shorter intervals.
          </li>
          <li>
            Use a dedicated &quot;incoming&quot; directory that gets cleaned up after
            processing to avoid re-triggering on old files.
          </li>
          <li>
            Pair monitors with jobs that include a <code className="rounded bg-muted px-1 py-0.5 text-xs">file.move</code>{" "}
            step to archive processed files.
          </li>
        </ul>
      </section>

      <GuidePrevNext
        prev={{ label: "Chains", href: "/guide/chains" }}
        next={{ label: "Notifications", href: "/guide/notifications" }}
      />
    </div>
  );
}
