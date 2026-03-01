"use client";

import { useState } from "react";
import { FolderOpen, HelpCircle } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
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
import { FileBrowserDialog } from "@/components/shared/file-browser-dialog";
import { usePgpKeys } from "@/lib/hooks/use-pgp-keys";

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

function PgpKeyPicker({
  label,
  tooltip,
  value,
  onChange,
  keyTypeFilter,
}: {
  label: string;
  tooltip: string;
  value: string;
  onChange: (id: string) => void;
  keyTypeFilter?: string;
}) {
  const { data: keysData } = usePgpKeys(1, 100, { status: "active", keyType: keyTypeFilter });
  const keys = keysData?.data ?? [];

  return (
    <div className="grid gap-1.5">
      <div className="flex items-center gap-1.5">
        <Label className="text-xs">{label}</Label>
        <FieldTooltip text={tooltip} />
      </div>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger>
          <SelectValue placeholder="Select key" />
        </SelectTrigger>
        <SelectContent>
          {keys.map((key) => (
            <SelectItem key={key.id} value={key.id}>
              {key.name} {key.shortKeyId ? `(${key.shortKeyId})` : ""}
            </SelectItem>
          ))}
          {keys.length === 0 && (
            <SelectItem value="" disabled>
              No active PGP keys found
            </SelectItem>
          )}
        </SelectContent>
      </Select>
    </div>
  );
}

function PgpMultiKeyPicker({
  label,
  tooltip,
  values,
  onChange,
  keyTypeFilter,
}: {
  label: string;
  tooltip: string;
  values: string[];
  onChange: (ids: string[]) => void;
  keyTypeFilter?: string;
}) {
  const { data: keysData } = usePgpKeys(1, 100, { status: "active", keyType: keyTypeFilter });
  const keys = keysData?.data ?? [];

  function toggleKey(id: string) {
    if (values.includes(id)) {
      onChange(values.filter((v) => v !== id));
    } else {
      onChange([...values, id]);
    }
  }

  return (
    <div className="grid gap-1.5">
      <div className="flex items-center gap-1.5">
        <Label className="text-xs">{label}</Label>
        <FieldTooltip text={tooltip} />
      </div>
      <div className="rounded-md border p-2 max-h-40 overflow-y-auto space-y-1">
        {keys.length === 0 ? (
          <p className="text-xs text-muted-foreground py-1">No active PGP keys found</p>
        ) : (
          keys.map((key) => (
            <label key={key.id} className="flex items-center gap-2 text-sm cursor-pointer hover:bg-muted/50 rounded px-1 py-0.5">
              <input
                type="checkbox"
                checked={values.includes(key.id)}
                onChange={() => toggleKey(key.id)}
                className="rounded border"
              />
              <span>{key.name}</span>
              {key.shortKeyId && (
                <span className="text-xs text-muted-foreground font-mono">{key.shortKeyId}</span>
              )}
            </label>
          ))
        )}
      </div>
    </div>
  );
}

// --- PGP Encrypt ---

export interface PgpEncryptConfig {
  inputPath: string;
  outputPath: string;
  recipientKeyIds: string[];
  signingKeyId: string;
  outputFormat: string;
}

export function PgpEncryptForm({
  config,
  onChange,
}: {
  config: PgpEncryptConfig;
  onChange: (config: PgpEncryptConfig) => void;
}) {
  const [browsing, setBrowsing] = useState(false);

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">Input Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/plaintext/report.csv"
            value={config.inputPath}
            onChange={(e) => onChange({ ...config, inputPath: e.target.value })}
          />
          <Button type="button" variant="outline" size="icon" onClick={() => setBrowsing(true)} title="Browse filesystem">
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">Output Path</Label>
          <FieldTooltip text="Optional. Defaults to input path with .pgp extension." />
        </div>
        <Input
          placeholder="/data/encrypted/report.csv.pgp"
          value={config.outputPath}
          onChange={(e) => onChange({ ...config, outputPath: e.target.value })}
        />
      </div>
      <PgpMultiKeyPicker
        label="Recipient Keys"
        tooltip="Public keys of recipients who should be able to decrypt this file."
        values={config.recipientKeyIds}
        onChange={(ids) => onChange({ ...config, recipientKeyIds: ids })}
        keyTypeFilter="public_key"
      />
      <PgpKeyPicker
        label="Signing Key (optional)"
        tooltip="Private key to sign the encrypted data. Recipients can verify authenticity."
        value={config.signingKeyId}
        onChange={(id) => onChange({ ...config, signingKeyId: id })}
        keyTypeFilter="key_pair"
      />
      <div className="grid gap-1.5">
        <Label className="text-xs">Output Format</Label>
        <div className="flex gap-4">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="pgp-encrypt-format"
              value="binary"
              checked={config.outputFormat === "binary"}
              onChange={() => onChange({ ...config, outputFormat: "binary" })}
            />
            Binary
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="pgp-encrypt-format"
              value="armored"
              checked={config.outputFormat === "armored"}
              onChange={() => onChange({ ...config, outputFormat: "armored" })}
            />
            ASCII Armored
          </label>
        </div>
      </div>

      <FileBrowserDialog open={browsing} onOpenChange={setBrowsing} onSelect={(path) => onChange({ ...config, inputPath: path })} />
    </div>
  );
}

export function parsePgpEncryptConfig(configJson: string): PgpEncryptConfig {
  try {
    const p = JSON.parse(configJson);
    return {
      inputPath: p.input_path || "",
      outputPath: p.output_path || "",
      recipientKeyIds: p.recipient_key_ids || [],
      signingKeyId: p.signing_key_id || "",
      outputFormat: p.output_format || "binary",
    };
  } catch {
    return { inputPath: "", outputPath: "", recipientKeyIds: [], signingKeyId: "", outputFormat: "binary" };
  }
}

export function serializePgpEncryptConfig(config: PgpEncryptConfig): string {
  return JSON.stringify({
    input_path: config.inputPath || undefined,
    output_path: config.outputPath || undefined,
    recipient_key_ids: config.recipientKeyIds.length > 0 ? config.recipientKeyIds : undefined,
    signing_key_id: config.signingKeyId || undefined,
    output_format: config.outputFormat || undefined,
  });
}

// --- PGP Decrypt ---

export interface PgpDecryptConfig {
  inputPath: string;
  outputPath: string;
  privateKeyId: string;
  verifySignature: boolean;
}

export function PgpDecryptForm({
  config,
  onChange,
}: {
  config: PgpDecryptConfig;
  onChange: (config: PgpDecryptConfig) => void;
}) {
  const [browsing, setBrowsing] = useState(false);

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">Input Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/encrypted/report.csv.pgp"
            value={config.inputPath}
            onChange={(e) => onChange({ ...config, inputPath: e.target.value })}
          />
          <Button type="button" variant="outline" size="icon" onClick={() => setBrowsing(true)} title="Browse filesystem">
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">Output Path</Label>
          <FieldTooltip text="Optional. Defaults to input path without .pgp extension." />
        </div>
        <Input
          placeholder="/data/decrypted/report.csv"
          value={config.outputPath}
          onChange={(e) => onChange({ ...config, outputPath: e.target.value })}
        />
      </div>
      <PgpKeyPicker
        label="Private Key"
        tooltip="The private key to use for decryption. Must match one of the recipient keys used during encryption."
        value={config.privateKeyId}
        onChange={(id) => onChange({ ...config, privateKeyId: id })}
        keyTypeFilter="key_pair"
      />
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.verifySignature}
          onChange={(e) => onChange({ ...config, verifySignature: e.target.checked })}
          className="rounded border"
        />
        Verify signature (if signed)
      </label>

      <FileBrowserDialog open={browsing} onOpenChange={setBrowsing} onSelect={(path) => onChange({ ...config, inputPath: path })} />
    </div>
  );
}

export function parsePgpDecryptConfig(configJson: string): PgpDecryptConfig {
  try {
    const p = JSON.parse(configJson);
    return {
      inputPath: p.input_path || "",
      outputPath: p.output_path || "",
      privateKeyId: p.private_key_id || "",
      verifySignature: p.verify_signature ?? false,
    };
  } catch {
    return { inputPath: "", outputPath: "", privateKeyId: "", verifySignature: false };
  }
}

export function serializePgpDecryptConfig(config: PgpDecryptConfig): string {
  return JSON.stringify({
    input_path: config.inputPath || undefined,
    output_path: config.outputPath || undefined,
    private_key_id: config.privateKeyId || undefined,
    verify_signature: config.verifySignature || undefined,
  });
}

// --- PGP Sign ---

export interface PgpSignConfig {
  inputPath: string;
  outputPath: string;
  signingKeyId: string;
  mode: string;
  outputFormat: string;
}

export function PgpSignForm({
  config,
  onChange,
}: {
  config: PgpSignConfig;
  onChange: (config: PgpSignConfig) => void;
}) {
  const [browsing, setBrowsing] = useState(false);

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">Input Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/reports/report.csv"
            value={config.inputPath}
            onChange={(e) => onChange({ ...config, inputPath: e.target.value })}
          />
          <Button type="button" variant="outline" size="icon" onClick={() => setBrowsing(true)} title="Browse filesystem">
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">Output Path</Label>
          <FieldTooltip text="Optional. Defaults based on sign mode (e.g., .sig for detached)." />
        </div>
        <Input
          placeholder="/data/reports/report.csv.sig"
          value={config.outputPath}
          onChange={(e) => onChange({ ...config, outputPath: e.target.value })}
        />
      </div>
      <PgpKeyPicker
        label="Signing Key"
        tooltip="Private key used to create the signature."
        value={config.signingKeyId}
        onChange={(id) => onChange({ ...config, signingKeyId: id })}
        keyTypeFilter="key_pair"
      />
      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">Sign Mode</Label>
          <FieldTooltip text="Detached: separate .sig file. Inline: signature embedded in output. Clearsign: human-readable signed text." />
        </div>
        <Select value={config.mode} onValueChange={(v) => onChange({ ...config, mode: v })}>
          <SelectTrigger>
            <SelectValue placeholder="Select mode" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="detached">Detached</SelectItem>
            <SelectItem value="inline">Inline</SelectItem>
            <SelectItem value="clearsign">Clearsign</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-1.5">
        <Label className="text-xs">Output Format</Label>
        <div className="flex gap-4">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="pgp-sign-format"
              value="binary"
              checked={config.outputFormat === "binary"}
              onChange={() => onChange({ ...config, outputFormat: "binary" })}
            />
            Binary
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="pgp-sign-format"
              value="armored"
              checked={config.outputFormat === "armored"}
              onChange={() => onChange({ ...config, outputFormat: "armored" })}
            />
            ASCII Armored
          </label>
        </div>
      </div>

      <FileBrowserDialog open={browsing} onOpenChange={setBrowsing} onSelect={(path) => onChange({ ...config, inputPath: path })} />
    </div>
  );
}

export function parsePgpSignConfig(configJson: string): PgpSignConfig {
  try {
    const p = JSON.parse(configJson);
    return {
      inputPath: p.input_path || "",
      outputPath: p.output_path || "",
      signingKeyId: p.signing_key_id || "",
      mode: p.mode || "detached",
      outputFormat: p.output_format || "binary",
    };
  } catch {
    return { inputPath: "", outputPath: "", signingKeyId: "", mode: "detached", outputFormat: "binary" };
  }
}

export function serializePgpSignConfig(config: PgpSignConfig): string {
  return JSON.stringify({
    input_path: config.inputPath || undefined,
    output_path: config.outputPath || undefined,
    signing_key_id: config.signingKeyId || undefined,
    mode: config.mode || undefined,
    output_format: config.outputFormat || undefined,
  });
}

// --- PGP Verify ---

export interface PgpVerifyConfig {
  inputPath: string;
  signaturePath: string;
  signerKeyIds: string[];
}

export function PgpVerifyForm({
  config,
  onChange,
}: {
  config: PgpVerifyConfig;
  onChange: (config: PgpVerifyConfig) => void;
}) {
  const [browsing, setBrowsing] = useState<"input" | "signature" | null>(null);

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">Input Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/reports/report.csv"
            value={config.inputPath}
            onChange={(e) => onChange({ ...config, inputPath: e.target.value })}
          />
          <Button type="button" variant="outline" size="icon" onClick={() => setBrowsing("input")} title="Browse filesystem">
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label className="text-xs">Signature Path</Label>
          <FieldTooltip text="For detached signatures. Leave empty for inline/clearsign signatures." />
        </div>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/reports/report.csv.sig"
            value={config.signaturePath}
            onChange={(e) => onChange({ ...config, signaturePath: e.target.value })}
          />
          <Button type="button" variant="outline" size="icon" onClick={() => setBrowsing("signature")} title="Browse filesystem">
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <PgpMultiKeyPicker
        label="Signer Keys (optional)"
        tooltip="Restrict verification to these specific public keys. Leave empty to accept any known key."
        values={config.signerKeyIds}
        onChange={(ids) => onChange({ ...config, signerKeyIds: ids })}
        keyTypeFilter="public_key"
      />

      <FileBrowserDialog
        open={browsing !== null}
        onOpenChange={(open) => { if (!open) setBrowsing(null); }}
        onSelect={(path) => {
          if (browsing === "input") onChange({ ...config, inputPath: path });
          else if (browsing === "signature") onChange({ ...config, signaturePath: path });
        }}
      />
    </div>
  );
}

export function parsePgpVerifyConfig(configJson: string): PgpVerifyConfig {
  try {
    const p = JSON.parse(configJson);
    return {
      inputPath: p.input_path || "",
      signaturePath: p.signature_path || "",
      signerKeyIds: p.signer_key_ids || [],
    };
  } catch {
    return { inputPath: "", signaturePath: "", signerKeyIds: [] };
  }
}

export function serializePgpVerifyConfig(config: PgpVerifyConfig): string {
  return JSON.stringify({
    input_path: config.inputPath || undefined,
    signature_path: config.signaturePath || undefined,
    signer_key_ids: config.signerKeyIds.length > 0 ? config.signerKeyIds : undefined,
  });
}
