"use client";

import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useUpdateSshKey } from "@/lib/hooks/use-ssh-key-mutations";
import { toast } from "sonner";
import type { SshKeyDto, UpdateSshKeyRequest } from "@/lib/types";

interface SshKeyEditFormProps {
  sshKey: SshKeyDto;
}

export function SshKeyEditForm({ sshKey }: SshKeyEditFormProps) {
  const router = useRouter();
  const updateKey = useUpdateSshKey(sshKey.id);
  const { register, handleSubmit } = useForm<UpdateSshKeyRequest>({
    defaultValues: {
      name: sshKey.name,
      notes: sshKey.notes || "",
    },
  });

  const onSubmit = (data: UpdateSshKeyRequest) => {
    updateKey.mutate(data, {
      onSuccess: () => {
        toast.success("SSH key updated");
        router.push(`/keys/ssh/${sshKey.id}`);
      },
      onError: (error) => { toast.error(error.message); },
    });
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6 max-w-2xl">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Edit SSH Key</h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          Update metadata for {sshKey.name}
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
