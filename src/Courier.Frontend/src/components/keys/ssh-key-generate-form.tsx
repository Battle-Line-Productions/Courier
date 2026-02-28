"use client";

import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useGenerateSshKey } from "@/lib/hooks/use-ssh-key-mutations";
import { toast } from "sonner";
import type { GenerateSshKeyRequest } from "@/lib/types";

const keyTypes = [
  { value: "ed25519", label: "Ed25519 (recommended)" },
  { value: "rsa_4096", label: "RSA 4096" },
  { value: "rsa_2048", label: "RSA 2048" },
  { value: "ecdsa_256", label: "ECDSA P-256" },
];

export function SshKeyGenerateForm() {
  const router = useRouter();
  const generateKey = useGenerateSshKey();
  const { register, handleSubmit, setValue, watch, formState: { errors } } = useForm<GenerateSshKeyRequest>({
    defaultValues: { keyType: "ed25519" },
  });

  const onSubmit = (data: GenerateSshKeyRequest) => {
    generateKey.mutate(data, {
      onSuccess: (result) => {
        toast.success("SSH key generated");
        router.push(`/keys/ssh/${result.data!.id}`);
      },
      onError: (error) => { toast.error(error.message); },
    });
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6 max-w-2xl">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Generate SSH Key</h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          Generate a new SSH key pair for SFTP authentication
        </p>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">Key Settings</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">Name *</Label>
            <Input id="name" {...register("name", { required: "Name is required" })} placeholder="e.g. Production SFTP Key" />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>
          <div className="space-y-2">
            <Label>Key Type *</Label>
            <Select value={watch("keyType")} onValueChange={(v) => setValue("keyType", v)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {keyTypes.map((kt) => (
                  <SelectItem key={kt.value} value={kt.value}>{kt.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="passphrase">Passphrase</Label>
            <Input id="passphrase" {...register("passphrase")} type="password" placeholder="Optional passphrase for private key" />
          </div>
          <div className="space-y-2">
            <Label htmlFor="notes">Notes</Label>
            <Textarea id="notes" {...register("notes")} placeholder="Optional notes about this key" />
          </div>
        </CardContent>
      </Card>

      <div className="flex gap-3">
        <Button type="submit" disabled={generateKey.isPending}>
          {generateKey.isPending ? "Generating..." : "Generate Key"}
        </Button>
        <Button type="button" variant="outline" onClick={() => router.back()}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
