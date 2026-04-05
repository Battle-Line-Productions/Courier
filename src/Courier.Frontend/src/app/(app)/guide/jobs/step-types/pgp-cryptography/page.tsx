"use client";

import Link from "next/link";
import { StepCard, type StepDef } from "@/components/guide/step-card";
import { GuidePrevNext } from "@/components/guide/guide-nav";

const steps: StepDef[] = [
  {
    typeKey: "pgp.encrypt",
    name: "PGP Encrypt",
    description: "Encrypt a file using one or more PGP public keys managed in Courier.",
    fields: [
      { name: "input_path", type: "string", required: true, description: "File path to encrypt" },
      { name: "recipient_key_ids", type: "string[]", required: true, description: "PGP key IDs to encrypt for (at least one required)" },
      { name: "output_path", type: "string", required: false, description: "Output file path (default: input_path + \".pgp\")" },
      { name: "signing_key_id", type: "string", required: false, description: "PGP key ID for optional signing during encryption" },
      { name: "output_format", type: "string", required: false, description: "\"binary\" (default) or \"armored\" (ASCII-safe)" },
    ],
    outputs: ["encrypted_file"],
  },
  {
    typeKey: "pgp.decrypt",
    name: "PGP Decrypt",
    description: "Decrypt a PGP-encrypted file using a private key stored in Courier.",
    fields: [
      { name: "input_path", type: "string", required: true, description: "Encrypted file path to decrypt" },
      { name: "private_key_id", type: "string", required: true, description: "PGP private key ID for decryption" },
      { name: "output_path", type: "string", required: false, description: "Output file path (default: input_path + \".dec\")" },
      { name: "verify_signature", type: "boolean", required: false, description: "Verify embedded signature during decryption (default: false)" },
    ],
    outputs: ["decrypted_file"],
  },
  {
    typeKey: "pgp.sign",
    name: "PGP Sign",
    description: "Create a digital signature for a file using a PGP private key.",
    fields: [
      { name: "input_path", type: "string", required: true, description: "File path to sign" },
      { name: "signing_key_id", type: "string", required: true, description: "PGP private key ID for signing" },
      { name: "output_path", type: "string", required: false, description: "Output signature file path (default: input_path + \".sig\")" },
      { name: "mode", type: "string", required: false, description: "Signature mode: \"detached\" (default — separate .sig file), \"inline\" (signature embedded), or \"clearsign\" (readable text with signature)" },
    ],
    outputs: ["signature_file"],
  },
  {
    typeKey: "pgp.verify",
    name: "PGP Verify",
    description: "Verify a PGP signature on a file to confirm authenticity and integrity.",
    fields: [
      { name: "input_path", type: "string", required: true, description: "File path to verify" },
      { name: "detached_signature_path", type: "string", required: false, description: "Path to detached .sig file (if using detached signatures)" },
      { name: "expected_signer_key_id", type: "string", required: false, description: "Expected signer key ID to validate the signer's identity" },
    ],
    outputs: ["verify_status", "is_valid", "signer_fingerprint", "signature_timestamp"],
  },
];

export default function PgpCryptographyGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">PGP Cryptography Steps</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          4 step types for encrypting, decrypting, signing, and verifying files using
          PGP keys managed in the{" "}
          <Link href="/guide/keys" className="text-primary hover:underline">Keys</Link> page.
        </p>
        <p className="mt-2 text-sm text-muted-foreground">
          <Link href="/guide/jobs/step-types" className="text-primary hover:underline">&larr; Back to Step Types</Link>
        </p>
      </div>

      <div className="space-y-4">
        {steps.map((step) => (
          <StepCard key={step.typeKey} step={step} />
        ))}
      </div>

      <GuidePrevNext
        prev={{ label: "FTP / FTPS Transfer", href: "/guide/jobs/step-types/ftp-transfer" }}
        next={{ label: "Control Flow", href: "/guide/jobs/step-types/flow-control" }}
      />
    </div>
  );
}
