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

export default function AzureFunctionConnectionGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Azure Function Connection</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          HTTP-based connection for invoking Azure Functions. Unlike file transfer
          protocols, this connection type is used to trigger serverless functions as
          part of a job pipeline.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/connections" className="text-primary hover:underline">&larr; Back to Connections Guide</Link>
        </p>
      </div>

      <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
        <p className="text-sm"><strong>Default Port:</strong> 443 (HTTPS)</p>
      </div>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Basic Settings</h2>
        <FieldTable fields={[
          { name: "name", type: "string", required: true, description: "Display name for the connection (max 200 characters)" },
          { name: "group", type: "string", required: false, description: "Optional organizational grouping" },
          { name: "host", type: "string", required: true, description: "Azure Function App hostname (e.g., my-app.azurewebsites.net)" },
          { name: "port", type: "integer", required: false, description: "Port number (default: 443, almost always left as default)" },
          { name: "notes", type: "string", required: false, description: "Descriptive notes (max 2000 characters)" },
        ]} />
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Authentication</h2>
        <p className="text-sm text-muted-foreground">
          Azure Function connections always use <strong>Function Key</strong> authentication.
          The auth method and username are automatically set.
        </p>
        <FieldTable fields={[
          { name: "authMethod", type: "enum", required: false, description: "Auto-set to \"function_key\" — cannot be changed" },
          { name: "username", type: "string", required: false, description: "Auto-set to \"function_key\" — cannot be changed" },
          { name: "password", type: "string", required: true, description: "The Azure Function API key (host key or function key). Encrypted at rest." },
        ]} />
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>Finding your Function Key:</strong> In the Azure Portal, navigate to
            your Function App &rarr; App Keys &rarr; Host Keys for app-wide access, or
            under the specific function&apos;s Function Keys for scoped access.
          </p>
        </div>
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">How It Works</h2>
        <p className="text-sm text-muted-foreground">
          Azure Function connections are used by the{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">azure_function.execute</code>{" "}
          step type. The connection provides the hostname and API key; the step
          configuration specifies the function name and payload.
        </p>
        <h3 className="text-base font-medium">Two Execution Modes</h3>
        <ul className="list-inside list-disc space-y-2 text-sm text-muted-foreground">
          <li>
            <strong>Callback (default)</strong> — Courier sends a request to the function
            with a callback URL. The function processes the work and POSTs back a result
            when done. Ideal for long-running operations.
          </li>
          <li>
            <strong>Fire and Forget</strong> — Courier sends the request and considers
            the step successful if the function returns HTTP 2xx. No callback is expected.
            Use for simple triggers.
          </li>
        </ul>
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Not Available for Azure Functions</h2>
        <p className="text-sm text-muted-foreground">
          The following settings do not apply to Azure Function connections and are not
          shown in the form:
        </p>
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li>Host key verification (SSH-specific)</li>
          <li>SSH algorithms</li>
          <li>Passive mode (FTP-specific)</li>
          <li>TLS version and certificate policies (handled by Azure)</li>
          <li>Connection/operation timeouts and retries</li>
        </ul>
      </section>

      <GuidePrevNext
        prev={{ label: "FTPS Connection", href: "/guide/connections/ftps" }}
        next={{ label: "Keys", href: "/guide/keys" }}
      />
    </div>
  );
}
