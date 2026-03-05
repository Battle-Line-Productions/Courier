"use client";

import { KeyImportForm } from "@/components/keys/key-import-form";

export default function ImportPgpKeyPage() {
  return <KeyImportForm keyKind="pgp" />;
}
