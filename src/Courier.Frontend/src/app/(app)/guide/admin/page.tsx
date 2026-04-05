"use client";

import { GuideImage } from "@/components/guide/guide-image";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function AdminGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Administration</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          The Admin section is available to users with the Admin role. It provides user
          management, audit logging, authentication provider configuration, and system
          settings.
        </p>
      </div>

      {/* User Management */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">User Management</h2>
        <p className="text-sm text-muted-foreground">
          Navigate to <strong>Admin &rarr; Users</strong> to view and manage all user
          accounts. From here you can create new users, edit existing accounts, reset
          passwords, and control access.
        </p>
        <GuideImage
          src="/guide/screenshots/admin-users.png"
          alt="Admin Users page"
          caption="Manage user accounts, roles, and access"
        />
        <h3 className="text-base font-medium">User Roles</h3>
        <p className="text-sm text-muted-foreground">
          Courier uses three roles with increasing levels of access:
        </p>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-left">
                <th className="pb-2 pr-4 font-medium">Role</th>
                <th className="pb-2 font-medium">Permissions</th>
              </tr>
            </thead>
            <tbody className="text-muted-foreground">
              <tr className="border-b">
                <td className="py-2 pr-4 font-medium text-foreground">Viewer</td>
                <td className="py-2">Read-only access to all resources. Cannot create, edit, or trigger anything.</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4 font-medium text-foreground">Operator</td>
                <td className="py-2">Full access to jobs, connections, keys, chains, monitors, notifications, and tags. Can trigger jobs and manage schedules. Cannot access admin features.</td>
              </tr>
              <tr>
                <td className="py-2 pr-4 font-medium text-foreground">Admin</td>
                <td className="py-2">Full access to everything including user management, audit logs, authentication providers, and system settings.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      {/* Audit Log */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Audit Log</h2>
        <p className="text-sm text-muted-foreground">
          The Audit page provides a chronological record of every significant action
          taken in the system — who did what, when, and to which resource. This is
          essential for compliance, troubleshooting, and security monitoring.
        </p>
        <GuideImage
          src="/guide/screenshots/audit-log.png"
          alt="Audit Log page"
          caption="A complete audit trail of all system activity"
        />
        <p className="text-sm text-muted-foreground">
          Each audit entry includes the timestamp, user who performed the action, the
          action type (create, update, delete, trigger, etc.), the affected resource type,
          and details about what changed.
        </p>
      </section>

      {/* Security Settings */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Security Settings</h2>
        <p className="text-sm text-muted-foreground">
          The <strong>Security</strong> tab lets you configure session lifetimes, password
          policies, and account lockout behavior.
        </p>
        <GuideImage
          src="/guide/screenshots/admin-settings.png"
          alt="Admin Security settings"
          caption="Configure session tokens, password policy, and account lockout"
        />
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>
            <strong>Session</strong> — Set access token lifetime (minutes) and refresh
            token lifetime (days)
          </li>
          <li>
            <strong>Password Policy</strong> — Set minimum password length and complexity
            requirements
          </li>
          <li>
            <strong>Account Lockout</strong> — Configure maximum failed login attempts and
            lockout duration to protect against brute-force attacks
          </li>
        </ul>
      </section>

      {/* Account Security */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Account Security</h2>
        <p className="text-sm text-muted-foreground">
          Courier includes built-in account security features:
        </p>
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>
            <strong>Account lockout</strong> — After multiple failed login attempts, the
            account is temporarily locked to prevent brute-force attacks.
          </li>
          <li>
            <strong>Password requirements</strong> — Passwords must meet minimum complexity
            requirements (length, mixed case, numbers, special characters).
          </li>
          <li>
            <strong>Session management</strong> — Sessions expire after inactivity, and
            refresh tokens provide seamless re-authentication.
          </li>
          <li>
            <strong>SSO integration</strong> — Configure external identity providers
            (like Microsoft Entra ID) via <strong>Admin &rarr; Auth Providers</strong> for
            enterprise single sign-on.
          </li>
        </ul>
      </section>

      <GuidePrevNext
        prev={{ label: "Tags", href: "/guide/tags" }}
        next={{ label: "Functions SDK", href: "/guide/sdk" }}
      />
    </div>
  );
}
