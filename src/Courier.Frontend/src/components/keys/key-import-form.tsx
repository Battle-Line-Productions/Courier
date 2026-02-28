"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useImportPgpKey } from "@/lib/hooks/use-pgp-key-mutations";
import { useImportSshKey } from "@/lib/hooks/use-ssh-key-mutations";
import { toast } from "sonner";
import { Upload } from "lucide-react";

interface KeyImportFormProps {
  keyKind: "pgp" | "ssh";
}

export function KeyImportForm({ keyKind }: KeyImportFormProps) {
  const router = useRouter();
  const importPgp = useImportPgpKey();
  const importSsh = useImportSshKey();
  const [name, setName] = useState("");
  const [passphrase, setPassphrase] = useState("");
  const [file, setFile] = useState<File | null>(null);

  const isPending = keyKind === "pgp" ? importPgp.isPending : importSsh.isPending;
  const label = keyKind === "pgp" ? "PGP" : "SSH";

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name || !file) {
      toast.error("Name and key file are required");
      return;
    }

    const formData = new FormData();
    formData.append("name", name);
    if (passphrase) formData.append("passphrase", passphrase);
    formData.append("keyFile", file);

    const mutation = keyKind === "pgp" ? importPgp : importSsh;
    mutation.mutate(formData, {
      onSuccess: (result) => {
        toast.success(`${label} key imported`);
        router.push(`/keys/${keyKind}/${result.data!.id}`);
      },
      onError: (error) => { toast.error(error.message); },
    });
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6 max-w-2xl">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Import {label} Key</h1>
        <p className="text-sm text-muted-foreground mt-0.5">
          Upload an existing {label} key file
        </p>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">Key Details</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">Name *</Label>
            <Input id="name" value={name} onChange={(e) => setName(e.target.value)} placeholder={`e.g. Partner ${label} Key`} required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="keyFile">Key File *</Label>
            <div className="flex items-center gap-3">
              <Button type="button" variant="outline" onClick={() => document.getElementById("keyFile")?.click()} className="gap-2">
                <Upload className="h-4 w-4" />
                {file ? file.name : "Choose File"}
              </Button>
              <input id="keyFile" type="file" className="hidden" onChange={(e) => setFile(e.target.files?.[0] || null)} />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="passphrase">Passphrase</Label>
            <Input id="passphrase" type="password" value={passphrase} onChange={(e) => setPassphrase(e.target.value)} placeholder="Passphrase (if key is encrypted)" />
          </div>
        </CardContent>
      </Card>

      <div className="flex gap-3">
        <Button type="submit" disabled={isPending}>
          {isPending ? "Importing..." : `Import ${label} Key`}
        </Button>
        <Button type="button" variant="outline" onClick={() => router.back()}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
