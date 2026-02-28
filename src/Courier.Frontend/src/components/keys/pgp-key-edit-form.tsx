"use client";

import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useUpdatePgpKey } from "@/lib/hooks/use-pgp-key-mutations";
import { toast } from "sonner";
import type { PgpKeyDto, UpdatePgpKeyRequest } from "@/lib/types";

interface PgpKeyEditFormProps {
  pgpKey: PgpKeyDto;
}

export function PgpKeyEditForm({ pgpKey }: PgpKeyEditFormProps) {
  const router = useRouter();
  const updateKey = useUpdatePgpKey(pgpKey.id);
  const { register, handleSubmit } = useForm<UpdatePgpKeyRequest>({
    defaultValues: {
      name: pgpKey.name,
      purpose: pgpKey.purpose || "",
      notes: pgpKey.notes || "",
    },
  });

  const onSubmit = (data: UpdatePgpKeyRequest) => {
    updateKey.mutate(data, {
      onSuccess: () => {
        toast.success("PGP key updated");
        router.push(`/keys/pgp/${pgpKey.id}`);
      },
      onError: (error) => { toast.error(error.message); },
    });
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6 max-w-2xl">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Edit PGP Key</h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          Update metadata for {pgpKey.name}
        </p>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">Key Details</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" {...register("name")} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="purpose">Purpose</Label>
            <Input id="purpose" {...register("purpose")} placeholder="e.g. File encryption for partner X" />
          </div>
          <div className="space-y-2">
            <Label htmlFor="notes">Notes</Label>
            <Textarea id="notes" {...register("notes")} />
          </div>
        </CardContent>
      </Card>

      <div className="flex gap-3">
        <Button type="submit" disabled={updateKey.isPending}>
          {updateKey.isPending ? "Saving..." : "Save Changes"}
        </Button>
        <Button type="button" variant="outline" onClick={() => router.back()}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
