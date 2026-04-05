"use client";

export interface StepDef {
  typeKey: string;
  name: string;
  description: string;
  fields: { name: string; type: string; required: boolean; description: string }[];
  outputs: string[];
}

export function StepCard({ step }: { step: StepDef }) {
  return (
    <div id={step.typeKey.replace(".", "-")} className="scroll-mt-16 rounded-lg border p-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h3 className="text-base font-semibold">{step.name}</h3>
          <p className="mt-0.5 text-sm text-muted-foreground">{step.description}</p>
        </div>
        <code className="shrink-0 rounded bg-muted px-2 py-1 text-xs font-medium">
          {step.typeKey}
        </code>
      </div>

      {step.fields.length > 0 && (
        <div className="mt-4">
          <h4 className="text-sm font-medium text-muted-foreground">Configuration Fields</h4>
          <div className="mt-2 overflow-x-auto">
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
                {step.fields.map((f) => (
                  <tr key={f.name} className="border-b last:border-0">
                    <td className="py-2 pr-3">
                      <code className="rounded bg-muted px-1 py-0.5 text-xs">{f.name}</code>
                    </td>
                    <td className="py-2 pr-3 text-xs">{f.type}</td>
                    <td className="py-2 pr-3">
                      {f.required ? (
                        <span className="text-xs font-medium text-primary">Yes</span>
                      ) : (
                        <span className="text-xs text-muted-foreground">No</span>
                      )}
                    </td>
                    <td className="py-2 text-xs">{f.description}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {step.outputs.length > 0 && (
        <div className="mt-4">
          <h4 className="text-sm font-medium text-muted-foreground">Step Outputs</h4>
          <div className="mt-1 flex flex-wrap gap-1.5">
            {step.outputs.map((o) => (
              <code key={o} className="rounded bg-muted px-1.5 py-0.5 text-xs">
                {o}
              </code>
            ))}
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            Reference in later steps with{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">
              context:step-alias.{step.outputs[0]}
            </code>
          </p>
        </div>
      )}
    </div>
  );
}
