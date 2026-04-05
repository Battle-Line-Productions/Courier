"use client";

import Link from "next/link";
import { StepCard, type StepDef } from "@/components/guide/step-card";
import { GuidePrevNext } from "@/components/guide/guide-nav";

const steps: StepDef[] = [
  {
    typeKey: "azure_function.execute",
    name: "Azure Function Execute",
    description: "Invoke an Azure Function via HTTP. Supports both fire-and-forget and callback modes for long-running operations.",
    fields: [
      { name: "connection_id", type: "string", required: true, description: "ID of the Azure Function connection to use" },
      { name: "function_name", type: "string", required: true, description: "Name of the Azure Function to invoke" },
      { name: "input_payload", type: "string", required: false, description: "JSON payload to send to the function body" },
      { name: "wait_for_callback", type: "boolean", required: false, description: "Wait for the function to POST back a result (default: true)" },
      { name: "max_wait_sec", type: "integer", required: false, description: "Maximum seconds to wait for callback (default: 3600 — 1 hour)" },
      { name: "poll_interval_sec", type: "integer", required: false, description: "Seconds between database polls for callback result (default: 5)" },
    ],
    outputs: ["function_success", "callback_result", "http_status"],
  },
];

export default function AzureFunctionStepGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Azure Function Step</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Invoke Azure Functions as part of a job pipeline. Requires an{" "}
          <Link href="/guide/connections/azure-function" className="text-primary hover:underline">
            Azure Function connection
          </Link>{" "}
          configured with the function app hostname and API key.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/jobs/step-types" className="text-primary hover:underline">&larr; Back to Step Types</Link>
        </p>
      </div>

      <div className="space-y-4">
        {steps.map((step) => (
          <StepCard key={step.typeKey} step={step} />
        ))}
      </div>

      {/* Execution modes */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Execution Modes</h2>

        <div className="rounded-lg border p-5">
          <h3 className="text-base font-semibold">Callback Mode (default)</h3>
          <p className="mt-1 text-sm text-muted-foreground">
            When <code className="rounded bg-muted px-1 py-0.5 text-xs">wait_for_callback</code>{" "}
            is true (the default), Courier sends the function a request that includes a
            one-time callback URL. The function processes the work asynchronously, then
            POSTs the result back to Courier&apos;s callback endpoint.
          </p>
          <ol className="mt-3 list-inside list-decimal space-y-1 text-sm text-muted-foreground">
            <li>Courier calls the Azure Function with the payload + callback URL</li>
            <li>Function receives the request and begins processing</li>
            <li>Courier polls the database for the callback result</li>
            <li>Function POSTs result to the callback URL when done</li>
            <li>Step completes with the function&apos;s output in <code className="rounded bg-muted px-1 py-0.5 text-xs">callback_result</code></li>
          </ol>
          <div className="mt-3 rounded-lg border-l-4 border-primary/60 bg-primary/5 p-3">
            <p className="text-xs">
              The callback URL is secured with a one-time Bearer token. The function
              must include the <code className="rounded bg-muted px-1 py-0.5 text-xs">Courier.Functions.Sdk</code>{" "}
              NuGet package for easy callback handling.
            </p>
          </div>
        </div>

        <div className="rounded-lg border p-5">
          <h3 className="text-base font-semibold">Fire and Forget</h3>
          <p className="mt-1 text-sm text-muted-foreground">
            When <code className="rounded bg-muted px-1 py-0.5 text-xs">wait_for_callback</code>{" "}
            is false, Courier calls the function and considers the step successful if the
            function returns an HTTP 2xx status code. No callback is expected.
          </p>
          <p className="mt-2 text-sm text-muted-foreground">
            Use this mode for simple triggers where you don&apos;t need the function&apos;s
            output — for example, sending a notification or starting an external process.
          </p>
        </div>
      </section>

      <GuidePrevNext
        prev={{ label: "Control Flow", href: "/guide/jobs/step-types/flow-control" }}
        next={{ label: "Connections", href: "/guide/connections" }}
      />
    </div>
  );
}
