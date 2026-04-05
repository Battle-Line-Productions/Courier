"use client";

import { GuideImage } from "@/components/guide/guide-image";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function NotificationsGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Notifications</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Notification rules let you get alerted when important events happen — job
          failures, successful completions, monitor triggers, and more. Configure rules
          to send emails or other alerts based on the events you care about.
        </p>
      </div>

      {/* Notifications List */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Viewing Notification Rules</h2>
        <p className="text-sm text-muted-foreground">
          The Notifications page lists all configured rules with their name, entity type,
          event triggers, channel, and enabled status.
        </p>
        <GuideImage
          src="/guide/screenshots/notifications-list.png"
          alt="Notifications list page"
          caption="All notification rules with their trigger events and channels"
        />
      </section>

      {/* Creating a Notification Rule */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Creating a Notification Rule</h2>
        <p className="text-sm text-muted-foreground">
          Click <strong>+ Create Rule</strong> to set up a new notification. Configure the
          following:
        </p>
        <GuideImage
          src="/guide/screenshots/notification-create.png"
          alt="Create Notification Rule form"
          caption="Configure event triggers, channel, and recipients for a new notification rule"
        />
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>
            <strong>Name</strong> — A descriptive label (e.g., &quot;Alert on job
            failure&quot;)
          </li>
          <li>
            <strong>Entity Type</strong> — What kind of entity to watch (job, chain, or
            monitor)
          </li>
          <li>
            <strong>Event Types</strong> — Which events trigger the notification (e.g.,
            job_failed, job_completed, monitor_triggered)
          </li>
          <li>
            <strong>Channel</strong> — How to deliver the notification (email)
          </li>
          <li>
            <strong>Channel Configuration</strong> — Channel-specific settings like
            recipients and subject prefix
          </li>
        </ul>
      </section>

      {/* Event Types */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Available Event Types</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-left">
                <th className="pb-2 pr-4 font-medium">Entity</th>
                <th className="pb-2 pr-4 font-medium">Event</th>
                <th className="pb-2 font-medium">Description</th>
              </tr>
            </thead>
            <tbody className="text-muted-foreground">
              <tr className="border-b">
                <td className="py-2 pr-4">Job</td>
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">job_completed</code></td>
                <td className="py-2">A job finished successfully</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4">Job</td>
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">job_failed</code></td>
                <td className="py-2">A job failed during execution</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4">Job</td>
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">job_started</code></td>
                <td className="py-2">A job began executing</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4">Chain</td>
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">chain_completed</code></td>
                <td className="py-2">A chain finished all member jobs</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4">Chain</td>
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">chain_failed</code></td>
                <td className="py-2">A chain stopped due to a member job failure</td>
              </tr>
              <tr>
                <td className="py-2 pr-4">Monitor</td>
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">monitor_triggered</code></td>
                <td className="py-2">A monitor detected a matching file</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      {/* Testing */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Testing Notifications</h2>
        <p className="text-sm text-muted-foreground">
          After creating a rule, you can send a test notification from the rule&apos;s
          detail page to verify that the channel configuration is correct and the
          recipients receive the alert.
        </p>
      </section>

      {/* Notification Logs */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Notification Logs</h2>
        <p className="text-sm text-muted-foreground">
          Navigate to <strong>Notifications &rarr; Logs</strong> to see a history of all
          sent notifications, including delivery status and timestamps. This helps
          troubleshoot delivery issues.
        </p>
      </section>

      <GuidePrevNext
        prev={{ label: "Monitors", href: "/guide/monitors" }}
        next={{ label: "Tags", href: "/guide/tags" }}
      />
    </div>
  );
}
