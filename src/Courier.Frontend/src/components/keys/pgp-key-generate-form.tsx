"use client";

import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useGeneratePgpKey } from "@/lib/hooks/use-pgp-key-mutations";
import { toast } from "sonner";
import type { GeneratePgpKeyRequest } from "@/lib/types";

const algorithms = [
  { value: "rsa_4096", label: "RSA 4096" },
  { value: "rsa_3072", label: "RSA 3072" },
  { value: "rsa_2048", label: "RSA 2048" },
  { value: "ecc_curve25519", label: "ECC Curve25519" },
  { value: "ecc_p256", label: "ECC P-256" },
  { value: "ecc_p384", label: "ECC P-384" },
];

export function PgpKeyGenerateForm() {
  const router = useRouter();
  const generateKey = useGeneratePgpKey();
  const { register, handleSubmit, setValue, watch, formState: { errors } } = useForm<GeneratePgpKeyRequest>({
    defaultValues: { algorithm: "rsa_4096" },
  });

  const onSubmit = (data: GeneratePgpKeyRequest) => {
    generateKey.mutate(data, {
      onSuccess: (result) => {
        toast.success("PGP key generated");
        router.push(`/keys/pgp/${result.data!.id}`);
      },
      onError: (error) => { toast.error(error.message); },
    });
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6 max-w-2xl">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Generate PGP Key</h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          Generate a new PGP key pair for encryption and signing
        </p>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">Key Settings</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">Name *</Label>
            <Input id="name" {...register("name", { required: "Name is required" })} placeholder="e.g. Production PGP Key" />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>
          <div className="space-y-2">
            <Label>Algorithm *</Label>
            <Select value={watch("algorithm")} onValueChange={(v) => setValue("algorithm", v)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {algorithms.map((a) => (
                  <SelectItem key={a.value} value={a.value}>{a.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="purpose">Purpose</Label>
            <Input id="purpose" {...register("purpose")} placeholder="e.g. File encryption for partner X" />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Identity (optional)</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="realName">Real Name</Label>
              <Input id="realName" {...register("realName")} placeholder="Courier System" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              <Input id="email" {...register("email")} type="email" placeholder="courier@example.com" />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Security</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="passphrase">Passphrase</Label>
            <Input id="passphrase" {...register("passphrase")} type="password" placeholder="Optional passphrase for private key" />
          </div>
          <div className="space-y-2">
            <Label htmlFor="expiresInDays">Expires In (days)</Label>
            <Input id="expiresInDays" {...register("expiresInDays", { valueAsNumber: true })} type="number" placeholder="e.g. 365" />
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
