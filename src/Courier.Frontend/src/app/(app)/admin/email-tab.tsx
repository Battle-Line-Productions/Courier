"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { useSmtpSettings, useUpdateSmtpSettings, useTestSmtpConnection } from "@/lib/hooks/use-settings";
import { usePermissions } from "@/lib/hooks/use-permissions";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";
import { CheckCircle2, XCircle, Loader2 } from "lucide-react";

interface SmtpFormState {
  host: string;
  port: number;
  useSsl: boolean;
  username: string;
  password: string;
  fromAddress: string;
  fromName: string;
}

export function EmailTab() {
  const { data, isLoading } = useSmtpSettings();
  const updateSettings = useUpdateSmtpSettings();
  const testConnection = useTestSmtpConnection();
  const { can } = usePermissions();
  const settings = data?.data;
  const canEdit = can("SettingsManage");

  const [form, setForm] = useState<SmtpFormState | null>(null);

  const currentForm = form ?? (settings ? {
    host: settings.host,
    port: settings.port,
    useSsl: settings.useSsl,
    username: settings.username,
    password: "",
    fromAddress: settings.fromAddress,
    fromName: settings.fromName,
  } : null);

  if (isLoading || !currentForm) {
    return <div className="text-sm text-muted-foreground">Loading settings...</div>;
  }

  async function handleSave() {
    if (!currentForm) return;
    try {
      await updateSettings.mutateAsync({
        host: currentForm.host,
        port: currentForm.port,
        useSsl: currentForm.useSsl,
        username: currentForm.username,
        password: currentForm.password || undefined,
        fromAddress: currentForm.fromAddress,
        fromName: currentForm.fromName,
      });
      setForm(null);
      toast.success("Email settings updated.");
    } catch (err) {
      if (err instanceof ApiClientError) {
        toast.error(err.message);
      } else {
        toast.error("Failed to update settings.");
      }
    }
  }

  async function handleTest() {
    try {
      const result = await testConnection.mutateAsync();
      if (result.data?.success) {
        toast.success("SMTP connection successful.");
      } else {
        toast.error(result.data?.errorMessage ?? "SMTP connection failed.");
      }
    } catch (err) {
      if (err instanceof ApiClientError) {
        toast.error(err.message);
      } else {
        toast.error("Failed to test connection.");
      }
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-sm font-medium">SMTP Server</h3>
        <p className="text-xs text-muted-foreground mb-3">Configure the mail server used for email notifications.</p>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="smtpHost">Host</Label>
            <Input
              id="smtpHost"
              placeholder="smtp.example.com"
              value={currentForm.host}
              onChange={(e) => setForm({ ...currentForm, host: e.target.value })}
              disabled={!canEdit}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="smtpPort">Port</Label>
            <Input
              id="smtpPort"
              type="number"
              min={1}
              max={65535}
              value={currentForm.port}
              onChange={(e) => setForm({ ...currentForm, port: parseInt(e.target.value) || 587 })}
              disabled={!canEdit}
            />
          </div>
        </div>
        <div className="flex items-center gap-2 mt-4">
          <Switch
            id="smtpSsl"
            checked={currentForm.useSsl}
            onCheckedChange={(checked) => setForm({ ...currentForm, useSsl: checked })}
            disabled={!canEdit}
          />
          <Label htmlFor="smtpSsl">Use SSL/TLS</Label>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium">Authentication</h3>
        <p className="text-xs text-muted-foreground mb-3">Credentials for SMTP authentication. Leave blank if your server does not require auth.</p>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="smtpUsername">Username</Label>
            <Input
              id="smtpUsername"
              placeholder="noreply@example.com"
              value={currentForm.username}
              onChange={(e) => setForm({ ...currentForm, username: e.target.value })}
              disabled={!canEdit}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="smtpPassword">Password</Label>
            <Input
              id="smtpPassword"
              type="password"
              placeholder={settings?.isConfigured ? "(unchanged)" : ""}
              value={currentForm.password}
              onChange={(e) => setForm({ ...currentForm, password: e.target.value })}
              disabled={!canEdit}
            />
          </div>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium">Sender</h3>
        <p className="text-xs text-muted-foreground mb-3">The &quot;From&quot; address and display name on outgoing emails.</p>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="smtpFromAddress">From Address</Label>
            <Input
              id="smtpFromAddress"
              type="email"
              placeholder="noreply@example.com"
              value={currentForm.fromAddress}
              onChange={(e) => setForm({ ...currentForm, fromAddress: e.target.value })}
              disabled={!canEdit}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="smtpFromName">From Name</Label>
            <Input
              id="smtpFromName"
              placeholder="Courier"
              value={currentForm.fromName}
              onChange={(e) => setForm({ ...currentForm, fromName: e.target.value })}
              disabled={!canEdit}
            />
          </div>
        </div>
      </div>

      {canEdit && (
        <div className="flex items-center gap-3">
          <Button onClick={handleSave} disabled={updateSettings.isPending}>
            {updateSettings.isPending ? "Saving..." : "Save Changes"}
          </Button>
          <Button variant="outline" onClick={handleTest} disabled={testConnection.isPending}>
            {testConnection.isPending ? (
              <>
                <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                Testing...
              </>
            ) : (
              "Test Connection"
            )}
          </Button>
        </div>
      )}

      {!settings?.isConfigured && (
        <div className="rounded-md border border-amber-200 bg-amber-50 dark:border-amber-800 dark:bg-amber-950 p-3">
          <p className="text-sm text-amber-800 dark:text-amber-200">
            Email notifications are disabled. Configure your SMTP server and save to enable email delivery.
          </p>
        </div>
      )}
    </div>
  );
}
