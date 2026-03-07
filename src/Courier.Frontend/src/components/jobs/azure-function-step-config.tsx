"use client";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
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
  pollIntervalSec: number;
  maxWaitSec: number;
  initialDelaySec: number;
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
          <FieldTooltip text="The Azure Function connection that defines the Function App URL and credentials." />
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
          <FieldTooltip text="Optional JSON payload passed to the function's input binding. The function receives this as the request body." />
        </div>
        <Textarea
          placeholder='e.g., {"batchId": "2024-01"}'
          value={config.inputPayload}
          onChange={(e) => onChange({ ...config, inputPayload: e.target.value })}
          rows={3}
        />
      </div>

      <div className="grid grid-cols-3 gap-3">
        <div className="grid gap-1.5">
          <div className="flex items-center gap-1.5">
            <Label className="text-xs">Poll Interval (sec)</Label>
            <FieldTooltip text="How often (in seconds) to check Application Insights for function completion. Lower values detect completion faster but use more API quota." />
          </div>
          <Input
            type="number"
            value={config.pollIntervalSec}
            onChange={(e) => onChange({ ...config, pollIntervalSec: Number(e.target.value) || 15 })}
          />
        </div>
        <div className="grid gap-1.5">
          <div className="flex items-center gap-1.5">
            <Label className="text-xs">Max Wait (sec)</Label>
            <FieldTooltip text="Maximum time (in seconds) to wait for the function to complete before the step fails. Set to match your function's expected maximum runtime." />
          </div>
          <Input
            type="number"
            value={config.maxWaitSec}
            onChange={(e) => onChange({ ...config, maxWaitSec: Number(e.target.value) || 3600 })}
          />
        </div>
        <div className="grid gap-1.5">
          <div className="flex items-center gap-1.5">
            <Label className="text-xs">Initial Delay (sec)</Label>
            <FieldTooltip text="Seconds to wait before the first completion check. Application Insights has a 1-5 minute ingestion delay, so the first poll should be delayed." />
          </div>
          <Input
            type="number"
            value={config.initialDelaySec}
            onChange={(e) => onChange({ ...config, initialDelaySec: Number(e.target.value) || 30 })}
          />
        </div>
      </div>
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
      pollIntervalSec: parsed.poll_interval_sec ?? 15,
      maxWaitSec: parsed.max_wait_sec ?? 3600,
      initialDelaySec: parsed.initial_delay_sec ?? 30,
    };
  } catch {
    return {
      connectionId: "",
      functionName: "",
      inputPayload: "",
      pollIntervalSec: 15,
      maxWaitSec: 3600,
      initialDelaySec: 30,
    };
  }
}

export function serializeAzureFunctionConfig(config: AzureFunctionStepConfig): string {
  return JSON.stringify({
    connection_id: config.connectionId || undefined,
    function_name: config.functionName || undefined,
    input_payload: config.inputPayload || undefined,
    poll_interval_sec: config.pollIntervalSec,
    max_wait_sec: config.maxWaitSec,
    initial_delay_sec: config.initialDelaySec,
  });
}
