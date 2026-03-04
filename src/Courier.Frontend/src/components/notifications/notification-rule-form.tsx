"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
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
import { Checkbox } from "@/components/ui/checkbox";
import { toast } from "sonner";
import { useCreateNotificationRule, useUpdateNotificationRule } from "@/lib/hooks/use-notification-mutations";
import type { NotificationRuleDto } from "@/lib/types";

const EVENT_TYPES = [
  { value: "job_completed", label: "Job Completed" },
  { value: "job_failed", label: "Job Failed" },
  { value: "job_cancelled", label: "Job Cancelled" },
  { value: "job_timed_out", label: "Job Timed Out" },
  { value: "step_failed", label: "Step Failed" },
];

const ENTITY_TYPES = [
  { value: "job", label: "Job" },
  { value: "monitor", label: "Monitor" },
  { value: "chain", label: "Chain" },
];

const CHANNELS = [
  { value: "webhook", label: "Webhook" },
  { value: "email", label: "Email" },
];

interface NotificationRuleFormProps {
  rule?: NotificationRuleDto;
}

export function NotificationRuleForm({ rule }: NotificationRuleFormProps) {
  const router = useRouter();
  const createRule = useCreateNotificationRule();
  const updateRule = useUpdateNotificationRule();
  const isEditing = !!rule;

  const existingConfig = rule?.channelConfig as Record<string, unknown> | undefined;

  const [name, setName] = useState(rule?.name ?? "");
  const [description, setDescription] = useState(rule?.description ?? "");
  const [entityType, setEntityType] = useState(rule?.entityType ?? "job");
  const [entityId, setEntityId] = useState(rule?.entityId ?? "");
  const [channel, setChannel] = useState(rule?.channel ?? "webhook");
  const [selectedEvents, setSelectedEvents] = useState<string[]>(rule?.eventTypes ?? []);
  const [isEnabled, setIsEnabled] = useState(rule?.isEnabled ?? true);

  // Webhook config
  const [webhookUrl, setWebhookUrl] = useState((existingConfig?.url as string) ?? "");
  const [webhookSecret, setWebhookSecret] = useState((existingConfig?.secret as string) ?? "");

  // Email config
  const [emailRecipients, setEmailRecipients] = useState(
    ((existingConfig?.recipients as string[]) ?? []).join(", ")
  );
  const [subjectPrefix, setSubjectPrefix] = useState(
    (existingConfig?.subjectPrefix as string) ?? "[Courier]"
  );

  function toggleEvent(event: string) {
    setSelectedEvents((prev) =>
      prev.includes(event) ? prev.filter((e) => e !== event) : [...prev, event]
    );
  }

  function buildChannelConfig(): Record<string, unknown> {
    if (channel === "webhook") {
      return {
        url: webhookUrl,
        ...(webhookSecret ? { secret: webhookSecret } : {}),
      };
    }
    return {
      recipients: emailRecipients.split(",").map((r) => r.trim()).filter(Boolean),
      subjectPrefix,
    };
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const data = {
      name,
      description: description || undefined,
      entityType,
      entityId: entityId || undefined,
      eventTypes: selectedEvents,
      channel,
      channelConfig: buildChannelConfig(),
      isEnabled,
    };

    if (isEditing) {
      updateRule.mutate(
        { id: rule.id, data: { ...data, isEnabled } },
        {
          onSuccess: () => {
            toast.success("Rule updated");
            router.push(`/notifications/${rule.id}`);
          },
          onError: (error) => toast.error(error.message),
        }
      );
    } else {
      createRule.mutate(data, {
        onSuccess: (res) => {
          toast.success("Rule created");
          router.push(`/notifications/${res.data?.id}`);
        },
        onError: (error) => toast.error(error.message),
      });
    }
  }

  const isPending = createRule.isPending || updateRule.isPending;

  return (
    <form onSubmit={handleSubmit} className="space-y-6 max-w-2xl">
      <div className="space-y-2">
        <Label htmlFor="name">Name</Label>
        <Input id="name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g., Job Failure Alert" required />
      </div>

      <div className="space-y-2">
        <Label htmlFor="description">Description</Label>
        <Textarea id="description" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Optional description" />
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label>Entity Type</Label>
          <Select value={entityType} onValueChange={setEntityType}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {ENTITY_TYPES.map((et) => (
                <SelectItem key={et.value} value={et.value}>{et.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label htmlFor="entityId">Entity ID (optional)</Label>
          <Input id="entityId" value={entityId} onChange={(e) => setEntityId(e.target.value)} placeholder="Leave empty for all" />
          <p className="text-xs text-muted-foreground">Leave empty to match all entities of this type</p>
        </div>
      </div>

      <div className="space-y-2">
        <Label>Event Types</Label>
        <div className="grid grid-cols-2 gap-2">
          {EVENT_TYPES.map((et) => (
            <label key={et.value} className="flex items-center gap-2 cursor-pointer">
              <Checkbox
                checked={selectedEvents.includes(et.value)}
                onCheckedChange={() => toggleEvent(et.value)}
              />
              <span className="text-sm">{et.label}</span>
            </label>
          ))}
        </div>
      </div>

      <div className="space-y-2">
        <Label>Channel</Label>
        <Select value={channel} onValueChange={setChannel}>
          <SelectTrigger><SelectValue /></SelectTrigger>
          <SelectContent>
            {CHANNELS.map((ch) => (
              <SelectItem key={ch.value} value={ch.value}>{ch.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {channel === "webhook" && (
        <div className="space-y-4 rounded-lg border p-4">
          <h3 className="font-medium text-sm">Webhook Configuration</h3>
          <div className="space-y-2">
            <Label htmlFor="webhookUrl">URL</Label>
            <Input id="webhookUrl" type="url" value={webhookUrl} onChange={(e) => setWebhookUrl(e.target.value)} placeholder="https://example.com/webhook" required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="webhookSecret">Secret (optional)</Label>
            <Input id="webhookSecret" value={webhookSecret} onChange={(e) => setWebhookSecret(e.target.value)} placeholder="HMAC signing secret" />
            <p className="text-xs text-muted-foreground">Used for HMAC-SHA256 signature in X-Courier-Signature header</p>
          </div>
        </div>
      )}

      {channel === "email" && (
        <div className="space-y-4 rounded-lg border p-4">
          <h3 className="font-medium text-sm">Email Configuration</h3>
          <div className="space-y-2">
            <Label htmlFor="recipients">Recipients</Label>
            <Input id="recipients" value={emailRecipients} onChange={(e) => setEmailRecipients(e.target.value)} placeholder="admin@example.com, ops@example.com" required />
            <p className="text-xs text-muted-foreground">Comma-separated email addresses</p>
          </div>
          <div className="space-y-2">
            <Label htmlFor="subjectPrefix">Subject Prefix</Label>
            <Input id="subjectPrefix" value={subjectPrefix} onChange={(e) => setSubjectPrefix(e.target.value)} />
          </div>
        </div>
      )}

      <div className="flex items-center gap-2">
        <Switch id="isEnabled" checked={isEnabled} onCheckedChange={setIsEnabled} />
        <Label htmlFor="isEnabled">Enabled</Label>
      </div>

      <div className="flex gap-3">
        <Button type="submit" disabled={isPending}>
          {isPending ? "Saving..." : isEditing ? "Update Rule" : "Create Rule"}
        </Button>
        <Button type="button" variant="outline" onClick={() => router.back()}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
