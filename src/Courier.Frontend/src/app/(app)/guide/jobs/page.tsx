"use client";

import Link from "next/link";
import { GuideImage } from "@/components/guide/guide-image";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function JobsGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Jobs</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Jobs are the core of Courier. Each job defines a sequence of steps that
          execute in order — downloading files, encrypting them, uploading to a remote
          server, and more.
        </p>
      </div>

      {/* Jobs List */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Viewing Jobs</h2>
        <p className="text-sm text-muted-foreground">
          The Jobs page lists all configured jobs with their name, description, version,
          enabled status, tags, and creation date. Use the search bar to filter by name,
          or use the tag dropdown to filter by tag.
        </p>
        <GuideImage
          src="/guide/screenshots/jobs-list.png"
          alt="Jobs list page"
          caption="The Jobs list showing all configured transfer jobs"
        />
      </section>

      {/* Creating a Job */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Creating a Job</h2>
        <p className="text-sm text-muted-foreground">
          Click the <strong>+ Create Job</strong> button in the top-right corner to create
          a new job. Provide a name and an optional description, then click{" "}
          <strong>Create</strong>.
        </p>
        <GuideImage
          src="/guide/screenshots/job-create.png"
          alt="Create Job form"
          caption="Enter a name and description for your new job"
        />
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>Tip:</strong> Use descriptive names like{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">daily-report-transfer</code>{" "}
            or{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">encrypted-backup-upload</code>.
            This makes it easier to identify jobs in the dashboard and audit log.
          </p>
        </div>
      </section>

      {/* Job Detail */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Job Detail View</h2>
        <p className="text-sm text-muted-foreground">
          Clicking a job name opens its detail view with multiple tabs:
        </p>
        <GuideImage
          src="/guide/screenshots/job-detail.png"
          alt="Job detail page"
          caption="The Job detail page with tabs for overview, steps, executions, and schedules"
        />
      </section>

      {/* Steps */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Configuring Steps</h2>
        <p className="text-sm text-muted-foreground">
          The <strong>Steps</strong> tab is where you define what the job actually does.
          Each step has a type, name, and configuration specific to that step type.
        </p>
        <GuideImage
          src="/guide/screenshots/job-steps.png"
          alt="Job steps tab"
          caption="Configure the sequence of steps that make up a job"
        />
        <h3 className="text-base font-medium">Available Step Types</h3>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-left">
                <th className="pb-2 pr-4 font-medium">Step Type</th>
                <th className="pb-2 font-medium">Description</th>
              </tr>
            </thead>
            <tbody className="text-muted-foreground">
              <tr className="border-b">
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">file.copy</code></td>
                <td className="py-2">Copy files from one location to another</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">file.move</code></td>
                <td className="py-2">Move files (copy then delete source)</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">sftp.upload</code></td>
                <td className="py-2">Upload files to an SFTP server</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">sftp.download</code></td>
                <td className="py-2">Download files from an SFTP server</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">pgp.encrypt</code></td>
                <td className="py-2">Encrypt files using a PGP key</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">pgp.decrypt</code></td>
                <td className="py-2">Decrypt PGP-encrypted files</td>
              </tr>
              <tr>
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">azure_function.execute</code></td>
                <td className="py-2">Invoke an Azure Function with optional callback</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div className="mt-4 rounded-lg border bg-muted/30 p-4">
          <p className="text-sm">
            <strong>Full Reference:</strong>{" "}
            <Link href="/guide/jobs/step-types" className="text-primary hover:underline">
              View the complete Step Type Reference &rarr;
            </Link>
            {" "}for all 29 step types with their configuration fields, required
            parameters, and output values.
          </p>
        </div>
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>Context References:</strong> Steps can reference outputs from previous
            steps using the <code className="rounded bg-muted px-1 py-0.5 text-xs">context:</code>{" "}
            prefix. For example,{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">context:download-report.downloaded_file</code>{" "}
            passes the output file path from a previous &quot;Download Report&quot; step.
          </p>
        </div>
      </section>

      {/* Executions */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Viewing Executions</h2>
        <p className="text-sm text-muted-foreground">
          The <strong>Executions</strong> tab shows the history of every time this job has
          run, including status, who triggered it, start time, and duration. Click an
          execution to see step-by-step details.
        </p>
        <GuideImage
          src="/guide/screenshots/job-executions.png"
          alt="Job executions tab"
          caption="Execution history showing past runs and their results"
        />
      </section>

      {/* Schedules */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Scheduling Jobs</h2>
        <p className="text-sm text-muted-foreground">
          The <strong>Schedules</strong> tab lets you create cron-based schedules to run
          the job automatically. You can add multiple schedules per job and enable or
          disable them independently.
        </p>
        <GuideImage
          src="/guide/screenshots/job-schedules.png"
          alt="Job schedules tab"
          caption="Set up cron schedules to automate job execution"
        />
        <h3 className="text-base font-medium">Common Cron Examples</h3>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-left">
                <th className="pb-2 pr-4 font-medium">Expression</th>
                <th className="pb-2 font-medium">Schedule</th>
              </tr>
            </thead>
            <tbody className="text-muted-foreground">
              <tr className="border-b">
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">0 0 * * *</code></td>
                <td className="py-2">Daily at midnight</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">0 */6 * * *</code></td>
                <td className="py-2">Every 6 hours</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">30 8 * * 1-5</code></td>
                <td className="py-2">Weekdays at 8:30 AM</td>
              </tr>
              <tr>
                <td className="py-2 pr-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">0 2 1 * *</code></td>
                <td className="py-2">First of every month at 2 AM</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      {/* Triggering */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Running a Job Manually</h2>
        <p className="text-sm text-muted-foreground">
          You can trigger any job on demand from the job detail page by clicking the{" "}
          <strong>Trigger</strong> button. The job will be queued and picked up by the
          Worker within a few seconds.
        </p>
      </section>

      <GuidePrevNext
        prev={{ label: "Getting Started", href: "/guide/getting-started" }}
        next={{ label: "Connections", href: "/guide/connections" }}
      />
    </div>
  );
}
