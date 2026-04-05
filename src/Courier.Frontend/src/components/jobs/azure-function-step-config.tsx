"use client";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { HelpCircle } from "lucide-react";
import { useConnections } from "@/lib/hooks/use-connections";

export interface AzureFunctionStepConfig {
  connectionId: string;
  functionName: string;
  inputPayload: string;
  waitForCallback: boolean;
  pollIntervalSec: number;
  maxWaitSec: number;
}

interface AzureFunctionStepConfigFormProps {
  config: AzureFunctionStepConfig;
  onChange: (config: AzureFunctionStepConfig) => void;
}

function FieldTooltip({ text }: { text: string }) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <HelpCircle className="h-3.5 w-3.5 text-muted-foreground cursor-help" />
      </TooltipTrigger>
      <TooltipContent side="top" className="max-w-xs">
        <p className="text-xs">{text}</p>
      </TooltipContent>
    </Tooltip>
  );
}

export function AzureFunctionStepConfigForm({ config, onChange }: AzureFunctionStepConfigFormProps) {
  const { data: connectionsData } = useConnections(1, 100, { protocol: "azure_function" });
  const connections = connectionsData?.data ?? [];

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">Connection</Label>
          <FieldTooltip text="The Azure Function connection that defines the Function App URL and function key." />
        </div>
        <Select
          value={config.connectionId}
          onValueChange={(v) => onChange({ ...config, connectionId: v })}
        >
          <SelectTrigger>
            <SelectValue placeholder="Select connection" />
          </SelectTrigger>
          <SelectContent>
            {connections.map((conn) => (
              <SelectItem key={conn.id} value={conn.id}>
                {conn.name} ({conn.host})
              </SelectItem>
            ))}
            {connections.length === 0 && (
              <SelectItem value="__none__" disabled>
                No Azure Function connections found
              </SelectItem>
            )}
          </SelectContent>
        </Select>
      </div>

      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">Function Name</Label>
          <FieldTooltip text="The exact name of the function to trigger (case-sensitive). Must be an HTTP-triggered function in the connected Function App." />
        </div>
        <Input
          placeholder="e.g., ProcessInvoices"
          value={config.functionName}
          onChange={(e) => onChange({ ...config, functionName: e.target.value })}
        />
      </div>

      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">Input Payload</Label>
          <FieldTooltip text="Optional JSON payload sent to the function. In callback mode, this is wrapped in a 'payload' property alongside the callback info." />
        </div>
        <Textarea
          placeholder='e.g., {"batchId": "2024-01"}'
          value={config.inputPayload}
          onChange={(e) => onChange({ ...config, inputPayload: e.target.value })}
          rows={3}
        />
      </div>

      <div className="flex items-center justify-between">
        <div className="space-y-0.5">
          <div className="flex items-center gap-1.5">
            <Label className="text-xs">Wait for Callback</Label>
            <FieldTooltip text="When enabled, the step waits for the function to report completion via the Courier SDK callback. When disabled, the step succeeds immediately after the HTTP trigger returns 2xx." />
          </div>
          <p className="text-xs text-muted-foreground">
            Requires the function to use the Courier.Functions.Sdk NuGet package.
          </p>
        </div>
        <Switch
          checked={config.waitForCallback}
          onCheckedChange={(checked) => onChange({ ...config, waitForCallback: checked })}
        />
      </div>

      {config.waitForCallback && (
        <div className="grid grid-cols-2 gap-3">
          <div className="grid gap-1.5">
            <div className="flex items-center gap-1.5">
              <Label className="text-xs">Poll Interval (sec)</Label>
              <FieldTooltip text="How often (in seconds) to check the local database for the function's callback response." />
            </div>
            <Input
              type="number"
              value={config.pollIntervalSec}
              onChange={(e) => onChange({ ...config, pollIntervalSec: Number(e.target.value) || 5 })}
            />
          </div>
          <div className="grid gap-1.5">
            <div className="flex items-center gap-1.5">
              <Label className="text-xs">Max Wait (sec)</Label>
              <FieldTooltip text="Maximum time (in seconds) to wait for the callback before the step fails. Set to match your function's expected maximum runtime." />
            </div>
            <Input
              type="number"
              value={config.maxWaitSec}
              onChange={(e) => onChange({ ...config, maxWaitSec: Number(e.target.value) || 3600 })}
            />
          </div>
        </div>
      )}
    </div>
  );
}

export function parseAzureFunctionConfig(configJson: string): AzureFunctionStepConfig {
  try {
    const parsed = JSON.parse(configJson);
    return {
      connectionId: parsed.connection_id || "",
      functionName: parsed.function_name || "",
      inputPayload: parsed.input_payload || "",
      waitForCallback: parsed.wait_for_callback ?? true,
      pollIntervalSec: parsed.poll_interval_sec ?? 5,
      maxWaitSec: parsed.max_wait_sec ?? 3600,
    };
  } catch {
    return {
      connectionId: "",
      functionName: "",
      inputPayload: "",
      waitForCallback: true,
      pollIntervalSec: 5,
      maxWaitSec: 3600,
    };
  }
}

export function serializeAzureFunctionConfig(config: AzureFunctionStepConfig): string {
  return JSON.stringify({
    connection_id: config.connectionId || undefined,
    function_name: config.functionName || undefined,
    input_payload: config.inputPayload || undefined,
    wait_for_callback: config.waitForCallback,
    poll_interval_sec: config.pollIntervalSec,
    max_wait_sec: config.maxWaitSec,
  });
}
