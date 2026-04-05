"use client";

import Link from "next/link";
import { GuideImage } from "@/components/guide/guide-image";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function ConnectionsGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Connections</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Connections define the remote servers and services that Courier communicates
          with during file transfers. You configure them once, then reference them from
          job steps.
        </p>
      </div>

      {/* Connections List */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Viewing Connections</h2>
        <p className="text-sm text-muted-foreground">
          The Connections page lists all configured connections with their name, protocol,
          host, and status. Connections are reusable — a single SFTP connection can be
          used by multiple jobs.
        </p>
        <GuideImage
          src="/guide/screenshots/connections-list.png"
          alt="Connections list page"
          caption="All configured connections with protocol and host information"
        />
      </section>

      {/* Creating a Connection */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Creating a Connection</h2>
        <p className="text-sm text-muted-foreground">
          Click <strong>+ Create Connection</strong> to add a new connection. The form
          adapts based on the protocol you select, showing only the fields relevant to
          that protocol.
        </p>
        <GuideImage
          src="/guide/screenshots/connection-create.png"
          alt="Create Connection form"
          caption="The connection form adapts to show protocol-specific fields"
        />

        <h3 className="text-base font-medium">Supported Protocols</h3>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-left">
                <th className="pb-2 pr-4 font-medium">Protocol</th>
                <th className="pb-2 pr-4 font-medium">Default Port</th>
                <th className="pb-2 font-medium">Description</th>
              </tr>
            </thead>
            <tbody className="text-muted-foreground">
              <tr className="border-b">
                <td className="py-2 pr-4 font-medium text-foreground">SFTP</td>
                <td className="py-2 pr-4">22</td>
                <td className="py-2">SSH File Transfer Protocol — encrypted, the most common choice</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4 font-medium text-foreground">FTP</td>
                <td className="py-2 pr-4">21</td>
                <td className="py-2">File Transfer Protocol — unencrypted, legacy systems</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4 font-medium text-foreground">FTPS</td>
                <td className="py-2 pr-4">990</td>
                <td className="py-2">FTP over TLS — encrypted FTP</td>
              </tr>
              <tr>
                <td className="py-2 pr-4 font-medium text-foreground">Azure Function</td>
                <td className="py-2 pr-4">443</td>
                <td className="py-2">HTTP endpoint for invoking Azure Functions</td>
              </tr>
            </tbody>
          </table>
        </div>

        <h3 className="text-base font-medium">Detailed Protocol Guides</h3>
        <p className="text-sm text-muted-foreground">
          Each protocol has different configuration fields. See the detailed guide for
          every field, default value, and option:
        </p>
        <div className="grid gap-3 sm:grid-cols-2">
          {[
            { href: "/guide/connections/sftp", title: "SFTP", desc: "SSH keys, host key verification, algorithms" },
            { href: "/guide/connections/ftp", title: "FTP", desc: "Password auth, passive mode" },
            { href: "/guide/connections/ftps", title: "FTPS", desc: "TLS version, certificate pinning" },
            { href: "/guide/connections/azure-function", title: "Azure Function", desc: "Function key auth, callback modes" },
          ].map((p) => (
            <Link
              key={p.href}
              href={p.href}
              className="rounded-lg border p-4 transition-colors hover:border-primary/40 hover:bg-primary/5"
            >
              <h4 className="text-sm font-semibold">{p.title}</h4>
              <p className="mt-1 text-xs text-muted-foreground">{p.desc}</p>
            </Link>
          ))}
        </div>
      </section>

      {/* Authentication Methods */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Authentication Methods</h2>
        <p className="text-sm text-muted-foreground">
          Depending on the protocol, you can choose from different authentication methods:
        </p>
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>
            <strong>Password</strong> — Username and password authentication (SFTP, FTP, FTPS)
          </li>
          <li>
            <strong>SSH Key</strong> — Public key authentication using an SSH key managed
            in Courier&apos;s key store (SFTP only)
          </li>
          <li>
            <strong>Function Key</strong> — Host key and function key for Azure Functions
          </li>
        </ul>
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>Security:</strong> All credentials are encrypted at rest using
            AES-256-GCM envelope encryption. Passwords and private keys are never stored
            in plain text.
          </p>
        </div>
      </section>

      {/* Connection Detail */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Connection Details</h2>
        <p className="text-sm text-muted-foreground">
          Click a connection name to view its details, edit settings, or test the
          connection. Use the <strong>Test Connection</strong> button to verify that
          Courier can reach the server with the configured credentials.
        </p>
        <GuideImage
          src="/guide/screenshots/connection-detail.png"
          alt="Connection detail page"
          caption="View and edit connection settings, and test connectivity"
        />
      </section>

      <GuidePrevNext
        prev={{ label: "Jobs", href: "/guide/jobs" }}
        next={{ label: "Keys", href: "/guide/keys" }}
      />
    </div>
  );
}
