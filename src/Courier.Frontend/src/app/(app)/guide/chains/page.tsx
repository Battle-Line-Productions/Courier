"use client";

import { GuideImage } from "@/components/guide/guide-image";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function ChainsGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Chains</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Chains let you link multiple jobs into a sequential pipeline. When a chain runs,
          it executes each member job in order, with optional dependency control so
          downstream jobs only run if upstream jobs succeed.
        </p>
      </div>

      {/* Chains List */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Viewing Chains</h2>
        <p className="text-sm text-muted-foreground">
          The Chains page lists all configured chains with their name, description, number
          of member jobs, and status.
        </p>
        <GuideImage
          src="/guide/screenshots/chains-list.png"
          alt="Chains list page"
          caption="All configured chains with member count and status"
        />
      </section>

      {/* Creating a Chain */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Creating a Chain</h2>
        <p className="text-sm text-muted-foreground">
          Click <strong>+ Create Chain</strong> to create a new chain. Provide a
          descriptive name and optional description.
        </p>
        <GuideImage
          src="/guide/screenshots/chain-create.png"
          alt="Create Chain form"
          caption="Create a new chain with a name and description"
        />
      </section>

      {/* Chain Detail */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Configuring Chain Members</h2>
        <p className="text-sm text-muted-foreground">
          After creating a chain, open its detail page to add member jobs. Each member has
          an execution order and can optionally depend on a previous member.
        </p>
        <GuideImage
          src="/guide/screenshots/chain-detail.png"
          alt="Chain detail page"
          caption="Configure the sequence of jobs in the chain"
        />
        <h3 className="text-base font-medium">Member Settings</h3>
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>
            <strong>Execution Order</strong> — The position in the chain (1 runs first, 2
            runs second, etc.)
          </li>
          <li>
            <strong>Depends On</strong> — Which prior member must complete before this one
            starts
          </li>
          <li>
            <strong>Run on Upstream Failure</strong> — If enabled, this member runs even
            if its dependency failed (useful for cleanup or notification jobs)
          </li>
        </ul>
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>Example:</strong> A nightly pipeline might chain three jobs: (1)
            Download reports from SFTP, (2) Encrypt the files with PGP, (3) Upload
            encrypted files to a backup server. If the download fails, the encryption and
            upload steps are skipped.
          </p>
        </div>
      </section>

      {/* Scheduling Chains */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Scheduling Chains</h2>
        <p className="text-sm text-muted-foreground">
          Like individual jobs, chains can have cron schedules. When the schedule fires,
          the entire chain runs from the first member to the last.
        </p>
      </section>

      <GuidePrevNext
        prev={{ label: "Keys", href: "/guide/keys" }}
        next={{ label: "Monitors", href: "/guide/monitors" }}
      />
    </div>
  );
}
