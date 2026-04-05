"use client";

import { GuideImage } from "@/components/guide/guide-image";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function GettingStartedGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Getting Started</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Log in, explore the dashboard, and learn how to navigate Courier.
        </p>
      </div>

      {/* Logging In */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Logging In</h2>
        <p className="text-sm text-muted-foreground">
          When you first open Courier, you&apos;ll see the login screen. Enter the
          username and password provided by your administrator and click{" "}
          <strong>Sign In</strong>.
        </p>
        <GuideImage
          src="/guide/screenshots/login-page.png"
          alt="Courier login page"
          caption="The Courier login screen"
        />
        <p className="text-sm text-muted-foreground">
          If your organization has configured single sign-on (SSO), you&apos;ll also see
          an option to sign in with your identity provider below the login form.
        </p>
      </section>

      {/* Dashboard */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">The Dashboard</h2>
        <p className="text-sm text-muted-foreground">
          After signing in, you arrive at the Dashboard. This is your at-a-glance overview
          of the entire system.
        </p>
        <GuideImage
          src="/guide/screenshots/dashboard.png"
          alt="Courier dashboard"
          caption="The Dashboard shows summary statistics, recent executions, and active monitors"
        />
        <p className="text-sm text-muted-foreground">The dashboard shows:</p>
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>
            <strong>Summary cards</strong> — Total counts of jobs, connections, monitors,
            PGP keys, SSH keys, and the last job execution status.
          </li>
          <li>
            <strong>Recent Executions</strong> — A table of the most recent job runs with
            their status, trigger source, start time, and duration.
          </li>
          <li>
            <strong>Active Monitors</strong> — A list of currently active file monitors
            and what they&apos;re watching.
          </li>
        </ul>
      </section>

      {/* Navigation */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Navigating the Interface</h2>
        <p className="text-sm text-muted-foreground">
          The left sidebar provides quick access to every section of Courier. You can
          collapse it to icon-only mode by clicking the <strong>Collapse</strong> button
          at the bottom.
        </p>
        <GuideImage
          src="/guide/screenshots/sidebar-collapsed.png"
          alt="Collapsed sidebar"
          caption="The sidebar can be collapsed to save space"
        />
        <p className="text-sm text-muted-foreground">
          The top bar shows breadcrumb navigation so you always know where you are. Your
          user menu in the top-right corner provides access to your account settings and
          sign-out.
        </p>
        <GuideImage
          src="/guide/screenshots/user-menu.png"
          alt="User menu dropdown"
          caption="Access your account settings and sign out from the user menu"
        />
      </section>

      {/* My Account */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">My Account</h2>
        <p className="text-sm text-muted-foreground">
          Click your name in the top-right corner and select <strong>My Account</strong>{" "}
          to view your profile. From here you can change your display name, email, and
          password.
        </p>
        <GuideImage
          src="/guide/screenshots/my-account.png"
          alt="My Account page"
          caption="The My Account page lets you update your profile and change your password"
        />
      </section>

      <GuidePrevNext
        prev={{ label: "Overview", href: "/guide" }}
        next={{ label: "Jobs", href: "/guide/jobs" }}
      />
    </div>
  );
}
