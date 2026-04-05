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

export default function FtpsConnectionGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">FTPS Connection</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          FTP over TLS/SSL — encrypted FTP using TLS certificates. A good choice
          when your server supports FTP but not SSH/SFTP.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/connections" className="text-primary hover:underline">&larr; Back to Connections Guide</Link>
        </p>
      </div>

      <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
        <p className="text-sm">
          <strong>Default Port:</strong> 990 (implicit TLS) or 21 (explicit TLS — auto-detected by port)
        </p>
      </div>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Basic Settings</h2>
        <FieldTable fields={[
          { name: "name", type: "string", required: true, description: "Display name for the connection (max 200 characters)" },
          { name: "group", type: "string", required: false, description: "Optional organizational grouping" },
          { name: "host", type: "string", required: true, description: "Hostname or IP address of the FTPS server" },
          { name: "port", type: "integer", required: false, description: "Port number (default: 990 for implicit TLS, use 21 for explicit TLS)" },
          { name: "notes", type: "string", required: false, description: "Descriptive notes (max 2000 characters)" },
        ]} />
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Authentication</h2>
        <FieldTable fields={[
          { name: "authMethod", type: "enum", required: true, description: "\"password\" — FTPS uses password authentication" },
          { name: "username", type: "string", required: true, description: "FTP username (max 100 characters)" },
          { name: "password", type: "string", required: true, description: "FTP password (max 500 chars, encrypted at rest)" },
        ]} />
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">TLS Settings</h2>
        <p className="text-sm text-muted-foreground">
          FTPS-specific settings for TLS encryption and certificate validation.
        </p>
        <FieldTable fields={[
          { name: "tlsVersionFloor", type: "enum", required: false, description: "Minimum TLS version: \"tls12\" (default) or \"tls13\"" },
          { name: "tlsCertPolicy", type: "enum", required: false, description: "Certificate validation: \"system_trust\" (default — uses OS CA store), \"insecure\" (skip validation), or \"pinned_thumbprint\" (exact match)" },
          { name: "tlsPinnedThumbprint", type: "string", required: false, description: "SHA-256 certificate thumbprint (required when tlsCertPolicy is \"pinned_thumbprint\")" },
        ]} />
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>Certificate Pinning:</strong> For maximum security, use
            &quot;pinned_thumbprint&quot; and provide the server&apos;s certificate
            SHA-256 thumbprint. This prevents MITM attacks even if a CA is compromised.
          </p>
        </div>
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">FTP-Specific Settings</h2>
        <FieldTable fields={[
          { name: "passiveMode", type: "boolean", required: false, description: "Use passive mode for data connections (default: true)" },
        ]} />
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Transport &amp; Timing</h2>
        <FieldTable fields={[
          { name: "connectTimeoutSec", type: "integer", required: false, description: "Connection timeout (default: 30, range: 1-300)" },
          { name: "operationTimeoutSec", type: "integer", required: false, description: "Per-operation timeout (default: 300, range: 1-3600)" },
          { name: "keepaliveIntervalSec", type: "integer", required: false, description: "Keepalive interval (default: 60)" },
          { name: "transportRetries", type: "integer", required: false, description: "Retry attempts (default: 2, range: 0-3)" },
        ]} />
      </section>

      <GuidePrevNext
        prev={{ label: "FTP Connection", href: "/guide/connections/ftp" }}
        next={{ label: "Azure Function Connection", href: "/guide/connections/azure-function" }}
      />
    </div>
  );
}
