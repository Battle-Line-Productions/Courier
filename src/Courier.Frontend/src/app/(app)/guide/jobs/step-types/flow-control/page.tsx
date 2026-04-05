"use client";

import Link from "next/link";
import { StepCard, type StepDef } from "@/components/guide/step-card";
import { GuidePrevNext } from "@/components/guide/guide-nav";

const steps: StepDef[] = [
  {
    typeKey: "flow.if",
    name: "If Condition",
    description: "Conditionally execute the following steps based on a comparison. Steps between flow.if and flow.else (or flow.end) only run when the condition is true.",
    fields: [
      { name: "left", type: "string", required: true, description: "Left operand — a literal value or context reference" },
      { name: "operator", type: "string", required: true, description: "Comparison operator: \"eq\" (equals), \"neq\" (not equals), \"gt\" (greater than), \"lt\" (less than), \"exists\" (value is non-empty)" },
      { name: "right", type: "string", required: false, description: "Right operand — required for all operators except \"exists\"" },
    ],
    outputs: [],
  },
  {
    typeKey: "flow.else",
    name: "Else Branch",
    description: "Marks the start of the else branch. Steps between flow.else and flow.end only run when the preceding flow.if condition was false. No configuration needed.",
    fields: [],
    outputs: [],
  },
  {
    typeKey: "flow.foreach",
    name: "For Each Loop",
    description: "Iterate over an array output from a previous step. Steps between flow.foreach and flow.end are executed once for each item in the array.",
    fields: [
      { name: "source", type: "string", required: true, description: "Context reference to an array output (e.g., \"context:list-files.file_list\")" },
    ],
    outputs: [],
  },
  {
    typeKey: "flow.end",
    name: "End Block",
    description: "Marks the end of an if/else or foreach block. Every flow.if and flow.foreach must have a matching flow.end. No configuration needed.",
    fields: [],
    outputs: [],
  },
];

export default function FlowControlGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Control Flow Steps</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          4 step types for adding conditional logic and loops to your job pipelines.
          These let you skip steps based on conditions or repeat steps for each file
          in a list.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/jobs/step-types" className="text-primary hover:underline">&larr; Back to Step Types</Link>
        </p>
      </div>

      <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
        <p className="text-sm">
          <strong>Block Structure:</strong> Control flow steps work in pairs.
          A <code className="rounded bg-muted px-1 py-0.5 text-xs">flow.if</code> or{" "}
          <code className="rounded bg-muted px-1 py-0.5 text-xs">flow.foreach</code>{" "}
          opens a block, and <code className="rounded bg-muted px-1 py-0.5 text-xs">flow.end</code>{" "}
          closes it. Steps inside the block are conditionally executed or looped.
        </p>
      </div>

      {/* Example */}
      <div className="rounded-lg border bg-muted/30 p-4">
        <h3 className="text-sm font-semibold">Example: Download all CSV files</h3>
        <ol className="mt-2 list-inside list-decimal space-y-1 text-sm text-muted-foreground">
          <li><code className="rounded bg-muted px-1 py-0.5 text-xs">sftp.list</code> — List remote directory (outputs file_list)</li>
          <li><code className="rounded bg-muted px-1 py-0.5 text-xs">flow.if</code> — Check if file_count &gt; 0</li>
          <li className="ml-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">flow.foreach</code> — Loop over file_list</li>
          <li className="ml-8"><code className="rounded bg-muted px-1 py-0.5 text-xs">sftp.download</code> — Download each file</li>
          <li className="ml-4"><code className="rounded bg-muted px-1 py-0.5 text-xs">flow.end</code> — End foreach</li>
          <li><code className="rounded bg-muted px-1 py-0.5 text-xs">flow.end</code> — End if</li>
        </ol>
      </div>

      <div className="space-y-4">
        {steps.map((step) => (
          <StepCard key={step.typeKey} step={step} />
        ))}
      </div>

      <GuidePrevNext
        prev={{ label: "PGP Cryptography", href: "/guide/jobs/step-types/pgp-cryptography" }}
        next={{ label: "Azure Functions", href: "/guide/jobs/step-types/azure-function" }}
      />
    </div>
  );
}
