"use client";

import { useRouter } from "next/navigation";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { useCreateConnection, useUpdateConnection } from "@/lib/hooks/use-connection-mutations";
import { toast } from "sonner";
import type { ConnectionDto } from "@/lib/types";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { useEffect } from "react";

const protocolDefaults: Record<string, number> = {
  sftp: 22,
  ftp: 21,
  ftps: 990,
};

const connectionSchema = z.object({
  name: z.string().min(1, "Name is required").max(100, "Name must be 100 characters or less"),
  group: z.string().max(100).optional(),
  protocol: z.enum(["sftp", "ftp", "ftps"], { message: "Protocol is required" }),
  host: z.string().min(1, "Host is required").max(255),
  port: z.number().int().min(1).max(65535),
  authMethod: z.enum(["password", "ssh_key", "password_ssh_key"], { message: "Auth method is required" }),
  username: z.string().min(1, "Username is required").max(100),
  password: z.string().max(500).optional(),
  sshKeyId: z.string().optional(),
  hostKeyPolicy: z.enum(["trust_on_first_use", "accept_any", "manual"]).optional(),
  sshAlgorithms: z.string().max(2000).optional(),
  passiveMode: z.boolean().optional(),
  tlsVersionFloor: z.enum(["tls12", "tls13"]).optional(),
  tlsCertPolicy: z.enum(["os_default", "accept_any", "pinned_thumbprint"]).optional(),
  tlsPinnedThumbprint: z.string().max(200).optional(),
  connectTimeoutSec: z.number().int().min(1).max(300),
  operationTimeoutSec: z.number().int().min(1).max(3600),
  keepaliveIntervalSec: z.number().int().min(0).max(300),
  transportRetries: z.number().int().min(0).max(10),
  status: z.enum(["active", "disabled"]).optional(),
  fipsOverride: z.boolean().optional(),
  notes: z.string().max(2000).optional(),
});

type ConnectionFormValues = z.infer<typeof connectionSchema>;

interface ConnectionFormProps {
  connection?: ConnectionDto;
}

export function ConnectionForm({ connection }: ConnectionFormProps) {
  const router = useRouter();
  const isEdit = !!connection;

  const {
    register,
    handleSubmit,
    control,
    watch,
    setValue,
    formState: { errors },
  } = useForm<ConnectionFormValues>({
    resolver: zodResolver(connectionSchema),
    defaultValues: {
      name: connection?.name ?? "",
      group: connection?.group ?? "",
      protocol: (connection?.protocol as ConnectionFormValues["protocol"]) ?? "sftp",
      host: connection?.host ?? "",
      port: connection?.port ?? 22,
      authMethod: (connection?.authMethod as ConnectionFormValues["authMethod"]) ?? "password",
      username: connection?.username ?? "",
      password: "",
      sshKeyId: connection?.sshKeyId ?? "",
      hostKeyPolicy: (connection?.hostKeyPolicy as ConnectionFormValues["hostKeyPolicy"]) ?? "trust_on_first_use",
      sshAlgorithms: connection?.sshAlgorithms ?? "",
      passiveMode: connection?.passiveMode ?? true,
      tlsVersionFloor: (connection?.tlsVersionFloor as ConnectionFormValues["tlsVersionFloor"]) ?? "tls12",
      tlsCertPolicy: (connection?.tlsCertPolicy as ConnectionFormValues["tlsCertPolicy"]) ?? "os_default",
      tlsPinnedThumbprint: connection?.tlsPinnedThumbprint ?? "",
      connectTimeoutSec: connection?.connectTimeoutSec ?? 30,
      operationTimeoutSec: connection?.operationTimeoutSec ?? 120,
      keepaliveIntervalSec: connection?.keepaliveIntervalSec ?? 15,
      transportRetries: connection?.transportRetries ?? 3,
      status: (connection?.status as ConnectionFormValues["status"]) ?? "active",
      fipsOverride: connection?.fipsOverride ?? false,
      notes: connection?.notes ?? "",
    },
  });

  const protocol = watch("protocol");
  const authMethod = watch("authMethod");
  const tlsCertPolicy = watch("tlsCertPolicy");

  // Auto-default port when protocol changes
  useEffect(() => {
    const currentPort = watch("port");
    const allDefaults = Object.values(protocolDefaults);
    if (!currentPort || allDefaults.includes(currentPort)) {
      setValue("port", protocolDefaults[protocol]);
    }
  }, [protocol, setValue, watch]);

  const createConnection = useCreateConnection();
  const updateConnection = useUpdateConnection(connection?.id ?? "");
  const isSubmitting = createConnection.isPending || updateConnection.isPending;

  const showPasswordField = authMethod === "password" || authMethod === "password_ssh_key";
  const showSshKeyField = authMethod === "ssh_key" || authMethod === "password_ssh_key";
  const isSftp = protocol === "sftp";
  const isFtpOrFtps = protocol === "ftp" || protocol === "ftps";

  async function onSubmit(values: ConnectionFormValues) {
    try {
      if (isEdit) {
        await updateConnection.mutateAsync(values);
        toast.success("Connection updated");
        router.push(`/connections/${connection.id}`);
      } else {
        const result = await createConnection.mutateAsync(values);
        toast.success("Connection created");
        router.push(`/connections/${result.data!.id}`);
      }
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : "Something went wrong";
      toast.error(message);
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" asChild>
          <Link href={isEdit ? `/connections/${connection.id}` : "/connections"}>
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </Button>
        <h1 className="text-2xl font-bold tracking-tight">
          {isEdit ? "Edit Connection" : "Create Connection"}
        </h1>
      </div>

      {/* Core Settings */}
      <Card>
        <CardHeader>
          <CardTitle>Core Settings</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-1.5">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="e.g., Production SFTP" {...register("name")} />
              {errors.name && (
                <p className="text-sm text-destructive">{errors.name.message}</p>
              )}
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="group">Group</Label>
              <Input id="group" placeholder="e.g., production" {...register("group")} />
              {errors.group && (
                <p className="text-sm text-destructive">{errors.group.message}</p>
              )}
            </div>
          </div>
          <div className="grid grid-cols-3 gap-4">
            <div className="grid gap-1.5">
              <Label>Protocol</Label>
              <Controller
                control={control}
                name="protocol"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select protocol" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="sftp">SFTP</SelectItem>
                      <SelectItem value="ftp">FTP</SelectItem>
                      <SelectItem value="ftps">FTPS</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.protocol && (
                <p className="text-sm text-destructive">{errors.protocol.message}</p>
              )}
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="host">Host</Label>
              <Input id="host" placeholder="e.g., sftp.example.com" {...register("host")} />
              {errors.host && (
                <p className="text-sm text-destructive">{errors.host.message}</p>
              )}
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="port">Port</Label>
              <Input id="port" type="number" {...register("port", { valueAsNumber: true })} />
              {errors.port && (
                <p className="text-sm text-destructive">{errors.port.message}</p>
              )}
            </div>
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="notes">Notes</Label>
            <Textarea id="notes" placeholder="Optional notes about this connection..." {...register("notes")} />
            {errors.notes && (
              <p className="text-sm text-destructive">{errors.notes.message}</p>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Authentication */}
      <Card>
        <CardHeader>
          <CardTitle>Authentication</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-1.5">
              <Label>Auth Method</Label>
              <Controller
                control={control}
                name="authMethod"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select auth method" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="password">Password</SelectItem>
                      <SelectItem value="ssh_key">SSH Key</SelectItem>
                      <SelectItem value="password_ssh_key">Password + SSH Key</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.authMethod && (
                <p className="text-sm text-destructive">{errors.authMethod.message}</p>
              )}
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="username">Username</Label>
              <Input id="username" placeholder="e.g., sftpuser" {...register("username")} />
              {errors.username && (
                <p className="text-sm text-destructive">{errors.username.message}</p>
              )}
            </div>
          </div>
          {showPasswordField && (
            <div className="grid gap-1.5">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                placeholder={isEdit ? "Leave blank to keep current password" : "Enter password"}
                {...register("password")}
              />
              {errors.password && (
                <p className="text-sm text-destructive">{errors.password.message}</p>
              )}
            </div>
          )}
          {showSshKeyField && (
            <div className="grid gap-1.5">
              <Label>SSH Key</Label>
              <Input disabled placeholder="Coming soon - SSH key selection" />
            </div>
          )}
        </CardContent>
      </Card>

      {/* SFTP Settings */}
      {isSftp && (
        <Card>
          <CardHeader>
            <CardTitle>SFTP Settings</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-1.5">
              <Label>Host Key Policy</Label>
              <Controller
                control={control}
                name="hostKeyPolicy"
                render={({ field }) => (
                  <Select value={field.value ?? "trust_on_first_use"} onValueChange={field.onChange}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="trust_on_first_use">Trust on First Use</SelectItem>
                      <SelectItem value="accept_any">Accept Any</SelectItem>
                      <SelectItem value="manual">Manual</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="sshAlgorithms">SSH Algorithms (JSON)</Label>
              <Textarea
                id="sshAlgorithms"
                placeholder='e.g., {"kex": ["curve25519-sha256"]}'
                {...register("sshAlgorithms")}
              />
              {errors.sshAlgorithms && (
                <p className="text-sm text-destructive">{errors.sshAlgorithms.message}</p>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* FTP/FTPS Settings */}
      {isFtpOrFtps && (
        <Card>
          <CardHeader>
            <CardTitle>FTP{protocol === "ftps" ? "S" : ""} Settings</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <Label>Passive Mode</Label>
                <p className="text-sm text-muted-foreground">Use passive mode for data connections</p>
              </div>
              <Controller
                control={control}
                name="passiveMode"
                render={({ field }) => (
                  <Switch checked={field.value ?? true} onCheckedChange={field.onChange} />
                )}
              />
            </div>
            {protocol === "ftps" && (
              <>
                <div className="grid grid-cols-2 gap-4">
                  <div className="grid gap-1.5">
                    <Label>TLS Version Floor</Label>
                    <Controller
                      control={control}
                      name="tlsVersionFloor"
                      render={({ field }) => (
                        <Select value={field.value ?? "tls12"} onValueChange={field.onChange}>
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="tls12">TLS 1.2</SelectItem>
                            <SelectItem value="tls13">TLS 1.3</SelectItem>
                          </SelectContent>
                        </Select>
                      )}
                    />
                  </div>
                  <div className="grid gap-1.5">
                    <Label>TLS Certificate Policy</Label>
                    <Controller
                      control={control}
                      name="tlsCertPolicy"
                      render={({ field }) => (
                        <Select value={field.value ?? "os_default"} onValueChange={field.onChange}>
                          <SelectTrigger>
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="os_default">OS Default</SelectItem>
                            <SelectItem value="accept_any">Accept Any</SelectItem>
                            <SelectItem value="pinned_thumbprint">Pinned Thumbprint</SelectItem>
                          </SelectContent>
                        </Select>
                      )}
                    />
                  </div>
                </div>
                {tlsCertPolicy === "pinned_thumbprint" && (
                  <div className="grid gap-1.5">
                    <Label htmlFor="tlsPinnedThumbprint">Pinned Thumbprint</Label>
                    <Input
                      id="tlsPinnedThumbprint"
                      placeholder="SHA-256 thumbprint"
                      {...register("tlsPinnedThumbprint")}
                    />
                    {errors.tlsPinnedThumbprint && (
                      <p className="text-sm text-destructive">{errors.tlsPinnedThumbprint.message}</p>
                    )}
                  </div>
                )}
              </>
            )}
          </CardContent>
        </Card>
      )}

      {/* Timeouts */}
      <Card>
        <CardHeader>
          <CardTitle>Timeouts</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="grid gap-1.5">
              <Label htmlFor="connectTimeoutSec">Connect (sec)</Label>
              <Input id="connectTimeoutSec" type="number" {...register("connectTimeoutSec", { valueAsNumber: true })} />
              {errors.connectTimeoutSec && (
                <p className="text-sm text-destructive">{errors.connectTimeoutSec.message}</p>
              )}
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="operationTimeoutSec">Operation (sec)</Label>
              <Input id="operationTimeoutSec" type="number" {...register("operationTimeoutSec", { valueAsNumber: true })} />
              {errors.operationTimeoutSec && (
                <p className="text-sm text-destructive">{errors.operationTimeoutSec.message}</p>
              )}
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="keepaliveIntervalSec">Keepalive (sec)</Label>
              <Input id="keepaliveIntervalSec" type="number" {...register("keepaliveIntervalSec", { valueAsNumber: true })} />
              {errors.keepaliveIntervalSec && (
                <p className="text-sm text-destructive">{errors.keepaliveIntervalSec.message}</p>
              )}
            </div>
            <div className="grid gap-1.5">
              <Label htmlFor="transportRetries">Retries</Label>
              <Input id="transportRetries" type="number" {...register("transportRetries", { valueAsNumber: true })} />
              {errors.transportRetries && (
                <p className="text-sm text-destructive">{errors.transportRetries.message}</p>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Advanced (edit only) */}
      {isEdit && (
        <Card>
          <CardHeader>
            <CardTitle>Advanced</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-1.5">
              <Label>Status</Label>
              <Controller
                control={control}
                name="status"
                render={({ field }) => (
                  <Select value={field.value ?? "active"} onValueChange={field.onChange}>
                    <SelectTrigger className="w-48">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="active">Active</SelectItem>
                      <SelectItem value="disabled">Disabled</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
            <div className="flex items-center justify-between">
              <div>
                <Label>FIPS Override</Label>
                <p className="text-sm text-muted-foreground">Force FIPS-compliant algorithms</p>
              </div>
              <Controller
                control={control}
                name="fipsOverride"
                render={({ field }) => (
                  <Switch checked={field.value ?? false} onCheckedChange={field.onChange} />
                )}
              />
            </div>
          </CardContent>
        </Card>
      )}

      <div className="flex justify-end gap-3">
        <Button variant="outline" type="button" asChild>
          <Link href={isEdit ? `/connections/${connection.id}` : "/connections"}>Cancel</Link>
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Saving..." : isEdit ? "Save Changes" : "Create Connection"}
        </Button>
      </div>
    </form>
  );
}
