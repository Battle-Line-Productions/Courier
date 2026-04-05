"use client";

import Link from "next/link";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function AzureFunctionsSdkGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Azure Functions SDK
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          The <code className="rounded bg-muted px-1 py-0.5 text-xs">Courier.Functions.Sdk</code>{" "}
          NuGet package lets your Azure Functions report results back to Courier. It
          provides a single class that handles both callback and fire-and-forget modes
          automatically.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/sdk" className="text-primary hover:underline">
            &larr; Back to SDK Overview
          </Link>
        </p>
      </div>

      {/* Installation */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Installation</h2>
        <p className="text-sm text-muted-foreground">
          Add the SDK to your Azure Functions project:
        </p>
        <pre className="overflow-x-auto rounded-lg border bg-muted/50 p-4 text-sm">
          <code>dotnet add package Courier.Functions.Sdk</code>
        </pre>
        <p className="text-sm text-muted-foreground">
          The package targets <code className="rounded bg-muted px-1 py-0.5 text-xs">netstandard2.0</code>{" "}
          for maximum compatibility with .NET 6+, .NET 8, and Azure Functions isolated
          worker projects. Its only dependency is{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">System.Text.Json</code>.
        </p>
      </section>

      {/* Quick Start */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Quick Start</h2>
        <p className="text-sm text-muted-foreground">
          Here&apos;s a complete Azure Function that receives work from Courier,
          processes it, and reports the result:
        </p>
        <pre className="overflow-x-auto rounded-lg border bg-muted/50 p-4 text-sm leading-relaxed">
          <code>{`using Courier.Functions.Sdk;

public class ProcessReportFunction
{
    [Function("ProcessReport")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        // 1. Parse the request from Courier
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var callback = CourierCallback.FromBody(body);

        try
        {
            // 2. Access the payload Courier sent
            var payload = callback.Payload;

            // 3. Do your work...
            var result = await DoExpensiveWork(payload);

            // 4. Report success (no-op in fire-and-forget mode)
            await callback.SuccessAsync(new { processedRecords = result.Count });
            return new OkResult();
        }
        catch (Exception ex)
        {
            // 5. Report failure (no-op in fire-and-forget mode)
            await callback.FailAsync(ex.Message);
            return new StatusCodeResult(500);
        }
    }
}`}</code>
        </pre>
      </section>

      {/* API Reference */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">API Reference</h2>
        <p className="text-sm text-muted-foreground">
          The SDK exposes a single public class:{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">CourierCallback</code>.
        </p>

        {/* FromBody */}
        <div className="rounded-lg border p-5">
          <h3 className="font-mono text-sm font-semibold">
            CourierCallback.FromBody(string requestBody)
          </h3>
          <p className="mt-2 text-sm text-muted-foreground">
            Static factory method. Parses the HTTP request body sent by Courier and
            extracts the payload and callback information.
          </p>
          <div className="mt-3 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left">
                  <th className="pb-2 pr-3 font-medium">Parameter</th>
                  <th className="pb-2 pr-3 font-medium">Type</th>
                  <th className="pb-2 font-medium">Description</th>
                </tr>
              </thead>
              <tbody className="text-muted-foreground">
                <tr>
                  <td className="py-2 pr-3"><code className="rounded bg-muted px-1 py-0.5 text-xs">requestBody</code></td>
                  <td className="py-2 pr-3 text-xs">string</td>
                  <td className="py-2 text-xs">The raw HTTP request body from the Azure Function trigger</td>
                </tr>
              </tbody>
            </table>
          </div>
          <p className="mt-2 text-xs text-muted-foreground">
            Returns a <code className="rounded bg-muted px-1 py-0.5 text-xs">CourierCallback</code>{" "}
            instance. Never returns null — returns a no-op instance if the body is empty,
            malformed, or missing callback info.
          </p>
        </div>

        {/* Payload */}
        <div className="rounded-lg border p-5">
          <h3 className="font-mono text-sm font-semibold">
            .Payload
          </h3>
          <p className="mt-2 text-sm text-muted-foreground">
            Read-only property. Contains the JSON payload from the Courier job step&apos;s{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">input_payload</code>{" "}
            configuration field.
          </p>
          <div className="mt-3 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left">
                  <th className="pb-2 pr-3 font-medium">Type</th>
                  <th className="pb-2 font-medium">Description</th>
                </tr>
              </thead>
              <tbody className="text-muted-foreground">
                <tr>
                  <td className="py-2 pr-3 text-xs">JsonElement?</td>
                  <td className="py-2 text-xs">The user-defined payload, or null if no payload was provided</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        {/* HasCallback */}
        <div className="rounded-lg border p-5">
          <h3 className="font-mono text-sm font-semibold">
            .HasCallback
          </h3>
          <p className="mt-2 text-sm text-muted-foreground">
            Read-only property. Indicates whether this request includes callback
            information.
          </p>
          <div className="mt-3 overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left">
                  <th className="pb-2 pr-3 font-medium">Value</th>
                  <th className="pb-2 font-medium">Meaning</th>
                </tr>
              </thead>
              <tbody className="text-muted-foreground">
                <tr className="border-b">
                  <td className="py-2 pr-3 text-xs font-medium text-foreground">true</td>
                  <td className="py-2 text-xs">Callback mode — SuccessAsync/FailAsync will POST results to Courier</td>
                </tr>
                <tr>
                  <td className="py-2 pr-3 text-xs font-medium text-foreground">false</td>
                  <td className="py-2 text-xs">Fire-and-forget mode — SuccessAsync/FailAsync are silent no-ops</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        {/* SuccessAsync */}
        <div className="rounded-lg border p-5">
          <h3 className="font-mono text-sm font-semibold">
            .SuccessAsync(object? output = null, CancellationToken ct = default)
          </h3>
          <p className="mt-2 text-sm text-muted-foreground">
            Reports successful completion to Courier. The optional{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">output</code> object
            is serialized to JSON and becomes available in subsequent job steps as the{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">callback_result</code>{" "}
            output.
          </p>
          <p className="mt-2 text-xs text-muted-foreground">
            Safe to call in fire-and-forget mode — silently does nothing.
          </p>
        </div>

        {/* FailAsync */}
        <div className="rounded-lg border p-5">
          <h3 className="font-mono text-sm font-semibold">
            .FailAsync(string errorMessage, CancellationToken ct = default)
          </h3>
          <p className="mt-2 text-sm text-muted-foreground">
            Reports a failure to Courier. The{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">errorMessage</code>{" "}
            is recorded in the job execution log.
          </p>
          <p className="mt-2 text-xs text-muted-foreground">
            Safe to call in fire-and-forget mode — silently does nothing.
          </p>
        </div>
      </section>

      {/* How It Works */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">How It Works</h2>

        <h3 className="text-base font-medium">Callback Mode (default)</h3>
        <p className="text-sm text-muted-foreground">
          When a job step uses{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">azure_function.execute</code>{" "}
          with <code className="rounded bg-muted px-1 py-0.5 text-xs">wait_for_callback: true</code>{" "}
          (the default), Courier sends the function a request containing both the
          user&apos;s payload and a one-time callback URL:
        </p>
        <pre className="overflow-x-auto rounded-lg border bg-muted/50 p-4 text-sm leading-relaxed">
          <code>{`// What Courier sends to your function:
{
    "payload": { /* your input_payload */ },
    "callback": {
        "url": "https://courier.example.com/api/v1/callbacks/{id}",
        "key": "{one-time-bearer-token}"
    }
}`}</code>
        </pre>
        <p className="text-sm text-muted-foreground">
          The SDK extracts this automatically. When you call{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">SuccessAsync()</code> or{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">FailAsync()</code>, the
          SDK POSTs the result to the callback URL with the Bearer token for
          authentication.
        </p>
        <p className="text-sm text-muted-foreground">
          Meanwhile, Courier polls its database waiting for the callback. Once your
          function reports back, the job step completes and the pipeline continues.
        </p>

        <h3 className="mt-4 text-base font-medium">Fire-and-Forget Mode</h3>
        <p className="text-sm text-muted-foreground">
          When{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">wait_for_callback: false</code>,
          Courier sends only the payload (no callback info). The SDK detects this
          automatically — <code className="rounded bg-muted px-1 py-0.5 text-xs">HasCallback</code>{" "}
          is <code className="rounded bg-muted px-1 py-0.5 text-xs">false</code>, and{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">SuccessAsync</code> /{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">FailAsync</code> become
          silent no-ops.
        </p>
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>Write your function once.</strong> The same code works for both modes.
            You don&apos;t need to check which mode you&apos;re in — the SDK handles it.
          </p>
        </div>
      </section>

      {/* Security */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Security</h2>
        <ul className="list-inside list-disc space-y-2 text-sm text-muted-foreground">
          <li>
            <strong>One-time callback key</strong> — Each callback gets a unique,
            randomly generated Bearer token. It can only be used once; replays are
            rejected with HTTP 409.
          </li>
          <li>
            <strong>Time-bounded</strong> — Callbacks expire after{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">max_wait_sec</code>{" "}
            (default 1 hour). Expired callbacks are rejected with HTTP 410.
          </li>
          <li>
            <strong>No JWT required</strong> — The callback endpoint uses one-time key
            auth instead of JWT tokens, so your Azure Function doesn&apos;t need to
            manage Courier credentials.
          </li>
          <li>
            <strong>Function key authentication</strong> — The connection to your Azure
            Function is authenticated using a Function Key, which is encrypted at rest
            in Courier&apos;s database.
          </li>
        </ul>
      </section>

      {/* Courier Configuration */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Courier Server Configuration</h2>
        <p className="text-sm text-muted-foreground">
          For callback mode, Courier needs to know its own public URL so it can
          construct the callback URL sent to the function. Set this in Courier&apos;s
          configuration:
        </p>
        <pre className="overflow-x-auto rounded-lg border bg-muted/50 p-4 text-sm leading-relaxed">
          <code>{`{
    "Courier": {
        "BaseUrl": "https://courier.example.com"
    }
}`}</code>
        </pre>
        <p className="text-sm text-muted-foreground">
          If this is missing and a step tries to use callback mode, the step will fail
          with an error message.
        </p>
      </section>

      {/* Step-by-step Setup */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">End-to-End Setup</h2>
        <ol className="list-inside list-decimal space-y-3 text-sm text-muted-foreground">
          <li>
            <strong>Create an Azure Function connection</strong> in Courier — set the
            host to your Function App domain and paste the Function Key as the password.
          </li>
          <li>
            <strong>Add the SDK</strong> to your Azure Functions project:{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">dotnet add package Courier.Functions.Sdk</code>
          </li>
          <li>
            <strong>Write your function</strong> using the pattern above — call{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">CourierCallback.FromBody()</code>{" "}
            to parse the request, do your work, then call{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">SuccessAsync()</code> or{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">FailAsync()</code>.
          </li>
          <li>
            <strong>Deploy the function</strong> to Azure.
          </li>
          <li>
            <strong>Create a job</strong> in Courier with an{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">azure_function.execute</code>{" "}
            step — select the connection, enter the function name, and optionally provide
            an input payload.
          </li>
          <li>
            <strong>Trigger the job</strong> and watch the execution complete when your
            function calls back.
          </li>
        </ol>
      </section>

      <GuidePrevNext
        prev={{ label: "SDK Overview", href: "/guide/sdk" }}
      />
    </div>
  );
}
