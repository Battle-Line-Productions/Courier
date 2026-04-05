"use client";

import Link from "next/link";
import { GuidePrevNext } from "@/components/guide/guide-nav";

function FieldTable({ fields }: { fields: Array<{ name: string; type: string; required: boolean; description: string }> }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left">
            <th className="pb-2 pr-3 font-medium">Field</th>
            <th className="pb-2 pr-3 font-medium">Type</th>
            <th className="pb-2 pr-3 font-medium">Required</th>
            <th className="pb-2 font-medium">Description</th>
          </tr>
        </thead>
        <tbody className="text-muted-foreground">
          {fields.map((f) => (
            <tr key={f.name} className="border-b last:border-0">
              <td className="py-2 pr-3"><code className="rounded bg-muted px-1 py-0.5 text-xs">{f.name}</code></td>
              <td className="py-2 pr-3 text-xs">{f.type}</td>
              <td className="py-2 pr-3">{f.required ? <span className="text-xs font-medium text-primary">Yes</span> : <span className="text-xs">No</span>}</td>
              <td className="py-2 text-xs">{f.description}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default function FtpConnectionGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">FTP Connection</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          File Transfer Protocol — unencrypted file transfer. Use only for legacy
          systems that do not support SFTP or FTPS. Data is transmitted in plain text.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/connections" className="text-primary hover:underline">&larr; Back to Connections Guide</Link>
        </p>
      </div>

      <div className="rounded-lg border-l-4 border-destructive/60 bg-destructive/5 p-4">
        <p className="text-sm">
          <strong>Warning:</strong> FTP transmits credentials and data in plain text.
          Use SFTP or FTPS whenever possible for secure transfers.
        </p>
      </div>

      <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
        <p className="text-sm"><strong>Default Port:</strong> 21</p>
      </div>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Basic Settings</h2>
        <FieldTable fields={[
          { name: "name", type: "string", required: true, description: "Display name for the connection (max 200 characters)" },
          { name: "group", type: "string", required: false, description: "Optional organizational grouping" },
          { name: "host", type: "string", required: true, description: "Hostname or IP address of the FTP server" },
          { name: "port", type: "integer", required: false, description: "Port number (default: 21)" },
          { name: "notes", type: "string", required: false, description: "Descriptive notes (max 2000 characters)" },
        ]} />
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Authentication</h2>
        <FieldTable fields={[
          { name: "authMethod", type: "enum", required: true, description: "\"password\" — FTP only supports password authentication" },
          { name: "username", type: "string", required: true, description: "FTP username (max 100 characters)" },
          { name: "password", type: "string", required: true, description: "FTP password (max 500 chars, encrypted at rest)" },
        ]} />
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">FTP-Specific Settings</h2>
        <FieldTable fields={[
          { name: "passiveMode", type: "boolean", required: false, description: "Use passive mode for data connections (default: true). Passive mode is required for most firewalled environments." },
        ]} />
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>Passive Mode:</strong> In passive mode, the client initiates all
            connections. This works through most firewalls and NAT. Only disable if
            your server requires active mode.
          </p>
        </div>
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Transport &amp; Timing</h2>
        <FieldTable fields={[
          { name: "connectTimeoutSec", type: "integer", required: false, description: "Connection timeout (default: 30, range: 1-300)" },
          { name: "operationTimeoutSec", type: "integer", required: false, description: "Per-operation timeout (default: 300, range: 1-3600)" },
          { name: "keepaliveIntervalSec", type: "integer", required: false, description: "Keepalive interval (default: 60)" },
          { name: "transportRetries", type: "integer", required: false, description: "Retry attempts on failure (default: 2, range: 0-3)" },
        ]} />
      </section>

      <GuidePrevNext
        prev={{ label: "SFTP Connection", href: "/guide/connections/sftp" }}
        next={{ label: "FTPS Connection", href: "/guide/connections/ftps" }}
      />
    </div>
  );
}
