"use client";

import Link from "next/link";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function SftpConnectionGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">SFTP Connection</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          SSH File Transfer Protocol — encrypted file transfer over SSH. This is the
          most common and recommended protocol for secure file transfers.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/connections" className="text-primary hover:underline">
            &larr; Back to Connections Guide
          </Link>
        </p>
      </div>

      <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
        <p className="text-sm">
          <strong>Default Port:</strong> 22
        </p>
      </div>

      {/* Basic Fields */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Basic Settings</h2>
        <FieldTable
          fields={[
            { name: "name", type: "string", required: true, description: "Display name for the connection (max 200 characters)" },
            { name: "group", type: "string", required: false, description: "Optional organizational grouping (max 100 characters)" },
            { name: "host", type: "string", required: true, description: "Hostname or IP address of the SFTP server" },
            { name: "port", type: "integer", required: false, description: "Port number (default: 22, range: 1-65535)" },
            { name: "notes", type: "string", required: false, description: "Descriptive notes about this connection (max 2000 characters)" },
          ]}
        />
      </section>

      {/* Authentication */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Authentication</h2>
        <p className="text-sm text-muted-foreground">
          SFTP supports three authentication methods:
        </p>
        <FieldTable
          fields={[
            { name: "authMethod", type: "enum", required: true, description: "\"password\", \"ssh_key\", or \"password_and_ssh_key\"" },
            { name: "username", type: "string", required: true, description: "SSH username (max 100 characters)" },
            { name: "password", type: "string", required: false, description: "Required if authMethod includes \"password\" (max 500 chars, encrypted at rest)" },
            { name: "sshKeyId", type: "uuid", required: false, description: "Required if authMethod includes \"ssh_key\" — references a key from the Keys page" },
          ]}
        />
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>SSH Key Auth:</strong> Generate an SSH key in{" "}
            <Link href="/guide/keys" className="text-primary hover:underline">Keys</Link>,
            copy the public key to the remote server&apos;s{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">~/.ssh/authorized_keys</code>,
            then select it here.
          </p>
        </div>
      </section>

      {/* Host Key Verification */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Host Key Verification</h2>
        <p className="text-sm text-muted-foreground">
          Controls how Courier verifies the SSH server&apos;s identity to prevent
          man-in-the-middle attacks.
        </p>
        <FieldTable
          fields={[
            { name: "hostKeyPolicy", type: "enum", required: false, description: "\"trust_on_first_use\" (default) — accepts on first connect, remembers fingerprint. \"always_trust\" — skips verification. \"manual\" — requires explicit fingerprint." },
            { name: "storedHostFingerprint", type: "string", required: false, description: "SHA-256 fingerprint of the server's host key (required when hostKeyPolicy is \"manual\")" },
            { name: "sshAlgorithms", type: "JSON string", required: false, description: "Restrict cipher/kex/mac/hostkey algorithms. JSON format, e.g. {\"kex\": [\"curve25519-sha256\"]}" },
          ]}
        />
      </section>

      {/* Timeouts */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Transport &amp; Timing</h2>
        <FieldTable
          fields={[
            { name: "connectTimeoutSec", type: "integer", required: false, description: "Connection timeout in seconds (default: 30, range: 1-300)" },
            { name: "operationTimeoutSec", type: "integer", required: false, description: "Per-operation timeout in seconds (default: 300, range: 1-3600)" },
            { name: "keepaliveIntervalSec", type: "integer", required: false, description: "SSH keepalive interval in seconds (default: 60)" },
            { name: "transportRetries", type: "integer", required: false, description: "Number of retry attempts on transport failure (default: 2, range: 0-3)" },
          ]}
        />
      </section>

      {/* Advanced */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Advanced</h2>
        <FieldTable
          fields={[
            { name: "fipsOverride", type: "boolean", required: false, description: "Enable FIPS-compliant algorithm selection (default: false)" },
          ]}
        />
      </section>

      <GuidePrevNext
        prev={{ label: "Connections", href: "/guide/connections" }}
        next={{ label: "FTP Connection", href: "/guide/connections/ftp" }}
      />
    </div>
  );
}

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
              <td className="py-2 pr-3">
                <code className="rounded bg-muted px-1 py-0.5 text-xs">{f.name}</code>
              </td>
              <td className="py-2 pr-3 text-xs">{f.type}</td>
              <td className="py-2 pr-3">
                {f.required ? (
                  <span className="text-xs font-medium text-primary">Yes</span>
                ) : (
                  <span className="text-xs">No</span>
                )}
              </td>
              <td className="py-2 text-xs">{f.description}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
