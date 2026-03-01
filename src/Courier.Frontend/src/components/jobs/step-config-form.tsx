"use client";

import { useState } from "react";
import { FolderOpen } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { FileBrowserDialog } from "@/components/shared/file-browser-dialog";
import {
  AzureFunctionStepConfigForm,
  parseAzureFunctionConfig,
  serializeAzureFunctionConfig,
} from "./azure-function-step-config";
import type { AzureFunctionStepConfig } from "./azure-function-step-config";
import {
  FileZipStepConfigForm,
  parseFileZipConfig,
  serializeFileZipConfig,
  FileUnzipStepConfigForm,
  parseFileUnzipConfig,
  serializeFileUnzipConfig,
  FileDeleteStepConfigForm,
  parseFileDeleteConfig,
  serializeFileDeleteConfig,
} from "./compression-step-config";
import type {
  FileZipStepConfig,
  FileUnzipStepConfig,
  FileDeleteStepConfig,
} from "./compression-step-config";
import {
  TransferUploadForm,
  parseTransferUploadConfig,
  serializeTransferUploadConfig,
  TransferDownloadForm,
  parseTransferDownloadConfig,
  serializeTransferDownloadConfig,
  TransferListForm,
  parseTransferListConfig,
  serializeTransferListConfig,
  TransferMkdirForm,
  parseTransferMkdirConfig,
  serializeTransferMkdirConfig,
  TransferRmdirForm,
  parseTransferRmdirConfig,
  serializeTransferRmdirConfig,
} from "./transfer-step-config";
import {
  PgpEncryptForm,
  parsePgpEncryptConfig,
  serializePgpEncryptConfig,
  PgpDecryptForm,
  parsePgpDecryptConfig,
  serializePgpDecryptConfig,
  PgpSignForm,
  parsePgpSignConfig,
  serializePgpSignConfig,
  PgpVerifyForm,
  parsePgpVerifyConfig,
  serializePgpVerifyConfig,
} from "./pgp-step-config";

// --- File step config (file.copy, file.move) ---

interface FileStepConfig {
  sourcePath: string;
  destinationPath: string;
  overwrite: boolean;
}

function FileStepConfigForm({
  config,
  onChange,
}: {
  config: FileStepConfig;
  onChange: (config: FileStepConfig) => void;
}) {
  const [browseTarget, setBrowseTarget] = useState<"source" | "destination" | null>(null);

  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">Source Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/incoming/"
            value={config.sourcePath}
            onChange={(e) => onChange({ ...config, sourcePath: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setBrowseTarget("source")}
            title="Browse filesystem"
          >
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <div className="grid gap-1.5">
        <Label className="text-xs">Destination Path</Label>
        <div className="flex gap-1.5">
          <Input
            placeholder="/data/processed/"
            value={config.destinationPath}
            onChange={(e) => onChange({ ...config, destinationPath: e.target.value })}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => setBrowseTarget("destination")}
            title="Browse filesystem"
          >
            <FolderOpen className="size-4" />
          </Button>
        </div>
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.overwrite}
          onChange={(e) => onChange({ ...config, overwrite: e.target.checked })}
          className="rounded border"
        />
        Overwrite existing files
      </label>

      <FileBrowserDialog
        open={browseTarget !== null}
        onOpenChange={(open) => { if (!open) setBrowseTarget(null); }}
        onSelect={(path) => {
          if (browseTarget === "source") {
            onChange({ ...config, sourcePath: path });
          } else if (browseTarget === "destination") {
            onChange({ ...config, destinationPath: path });
          }
        }}
      />
    </div>
  );
}

// --- Generic dispatcher ---

interface StepConfigFormProps {
  typeKey: string;
  config: StepConfig;
  onChange: (config: StepConfig) => void;
}

// Union config type used externally
export interface StepConfig {
  // File copy/move fields
  sourcePath?: string;
  destinationPath?: string;
  overwrite?: boolean;
  // File zip fields
  outputPath?: string;
  password?: string;
  // File unzip fields
  archivePath?: string;
  outputDirectory?: string;
  // File delete fields
  path?: string;
  failIfNotFound?: boolean;
  // Azure function fields
  connectionId?: string;
  functionName?: string;
  inputPayload?: string;
  pollIntervalSec?: number;
  maxWaitSec?: number;
  initialDelaySec?: number;
  // Transfer fields
  localPath?: string;
  remotePath?: string;
  atomicUpload?: boolean;
  atomicSuffix?: string;
  resumePartial?: boolean;
  filePattern?: string;
  deleteAfterDownload?: boolean;
  recursive?: boolean;
  // PGP fields
  inputPath?: string;
  recipientKeyIds?: string[];
  signingKeyId?: string;
  outputFormat?: string;
  privateKeyId?: string;
  verifySignature?: boolean;
  mode?: string;
  signaturePath?: string;
  signerKeyIds?: string[];
  // Raw JSON passthrough for unknown types
  [key: string]: unknown;
}

/** Returns the operation suffix from a typeKey: "sftp.upload" → "upload" */
function operationOf(typeKey: string): string {
  const parts = typeKey.split(".");
  return parts[parts.length - 1];
}

/** Returns true if the typeKey is a transfer protocol step */
function isTransferStep(typeKey: string): boolean {
  return typeKey.startsWith("sftp.") || typeKey.startsWith("ftp.") || typeKey.startsWith("ftps.");
}

export function StepConfigForm({ typeKey, config, onChange }: StepConfigFormProps) {
  if (typeKey === "azure_function.execute") {
    const azConfig: AzureFunctionStepConfig = {
      connectionId: (config.connectionId as string) ?? "",
      functionName: (config.functionName as string) ?? "",
      inputPayload: (config.inputPayload as string) ?? "",
      pollIntervalSec: (config.pollIntervalSec as number) ?? 15,
      maxWaitSec: (config.maxWaitSec as number) ?? 3600,
      initialDelaySec: (config.initialDelaySec as number) ?? 30,
    };
    return (
      <AzureFunctionStepConfigForm
        config={azConfig}
        onChange={(c) =>
          onChange({
            connectionId: c.connectionId,
            functionName: c.functionName,
            inputPayload: c.inputPayload,
            pollIntervalSec: c.pollIntervalSec,
            maxWaitSec: c.maxWaitSec,
            initialDelaySec: c.initialDelaySec,
          })
        }
      />
    );
  }

  if (typeKey === "file.zip") {
    const zipConfig: FileZipStepConfig = {
      sourcePath: (config.sourcePath as string) ?? "",
      outputPath: (config.outputPath as string) ?? "",
      password: (config.password as string) ?? "",
    };
    return (
      <FileZipStepConfigForm
        config={zipConfig}
        onChange={(c) =>
          onChange({
            sourcePath: c.sourcePath,
            outputPath: c.outputPath,
            password: c.password,
          })
        }
      />
    );
  }

  if (typeKey === "file.unzip") {
    const unzipConfig: FileUnzipStepConfig = {
      archivePath: (config.archivePath as string) ?? "",
      outputDirectory: (config.outputDirectory as string) ?? "",
      password: (config.password as string) ?? "",
    };
    return (
      <FileUnzipStepConfigForm
        config={unzipConfig}
        onChange={(c) =>
          onChange({
            archivePath: c.archivePath,
            outputDirectory: c.outputDirectory,
            password: c.password,
          })
        }
      />
    );
  }

  if (typeKey === "file.delete") {
    const deleteConfig: FileDeleteStepConfig = {
      path: (config.path as string) ?? "",
      failIfNotFound: (config.failIfNotFound as boolean) ?? false,
    };
    return (
      <FileDeleteStepConfigForm
        config={deleteConfig}
        onChange={(c) =>
          onChange({
            path: c.path,
            failIfNotFound: c.failIfNotFound,
          })
        }
      />
    );
  }

  // Transfer steps (sftp/ftp/ftps)
  if (isTransferStep(typeKey)) {
    const op = operationOf(typeKey);

    if (op === "upload") {
      return (
        <TransferUploadForm
          typeKey={typeKey}
          config={{
            connectionId: (config.connectionId as string) ?? "",
            localPath: (config.localPath as string) ?? "",
            remotePath: (config.remotePath as string) ?? "",
            atomicUpload: (config.atomicUpload as boolean) ?? false,
            atomicSuffix: (config.atomicSuffix as string) ?? ".tmp",
            resumePartial: (config.resumePartial as boolean) ?? false,
          }}
          onChange={(c) =>
            onChange({
              connectionId: c.connectionId,
              localPath: c.localPath,
              remotePath: c.remotePath,
              atomicUpload: c.atomicUpload,
              atomicSuffix: c.atomicSuffix,
              resumePartial: c.resumePartial,
            })
          }
        />
      );
    }

    if (op === "download") {
      return (
        <TransferDownloadForm
          typeKey={typeKey}
          config={{
            connectionId: (config.connectionId as string) ?? "",
            remotePath: (config.remotePath as string) ?? "",
            localPath: (config.localPath as string) ?? "",
            filePattern: (config.filePattern as string) ?? "",
            resumePartial: (config.resumePartial as boolean) ?? false,
            deleteAfterDownload: (config.deleteAfterDownload as boolean) ?? false,
          }}
          onChange={(c) =>
            onChange({
              connectionId: c.connectionId,
              remotePath: c.remotePath,
              localPath: c.localPath,
              filePattern: c.filePattern,
              resumePartial: c.resumePartial,
              deleteAfterDownload: c.deleteAfterDownload,
            })
          }
        />
      );
    }

    if (op === "list") {
      return (
        <TransferListForm
          typeKey={typeKey}
          config={{
            connectionId: (config.connectionId as string) ?? "",
            remotePath: (config.remotePath as string) ?? "",
            filePattern: (config.filePattern as string) ?? "",
          }}
          onChange={(c) =>
            onChange({
              connectionId: c.connectionId,
              remotePath: c.remotePath,
              filePattern: c.filePattern,
            })
          }
        />
      );
    }

    if (op === "mkdir") {
      return (
        <TransferMkdirForm
          typeKey={typeKey}
          config={{
            connectionId: (config.connectionId as string) ?? "",
            remotePath: (config.remotePath as string) ?? "",
          }}
          onChange={(c) =>
            onChange({
              connectionId: c.connectionId,
              remotePath: c.remotePath,
            })
          }
        />
      );
    }

    if (op === "rmdir") {
      return (
        <TransferRmdirForm
          typeKey={typeKey}
          config={{
            connectionId: (config.connectionId as string) ?? "",
            remotePath: (config.remotePath as string) ?? "",
            recursive: (config.recursive as boolean) ?? false,
          }}
          onChange={(c) =>
            onChange({
              connectionId: c.connectionId,
              remotePath: c.remotePath,
              recursive: c.recursive,
            })
          }
        />
      );
    }
  }

  // PGP steps
  if (typeKey === "pgp.encrypt") {
    return (
      <PgpEncryptForm
        config={{
          inputPath: (config.inputPath as string) ?? "",
          outputPath: (config.outputPath as string) ?? "",
          recipientKeyIds: (config.recipientKeyIds as string[]) ?? [],
          signingKeyId: (config.signingKeyId as string) ?? "",
          outputFormat: (config.outputFormat as string) ?? "binary",
        }}
        onChange={(c) =>
          onChange({
            inputPath: c.inputPath,
            outputPath: c.outputPath,
            recipientKeyIds: c.recipientKeyIds,
            signingKeyId: c.signingKeyId,
            outputFormat: c.outputFormat,
          })
        }
      />
    );
  }

  if (typeKey === "pgp.decrypt") {
    return (
      <PgpDecryptForm
        config={{
          inputPath: (config.inputPath as string) ?? "",
          outputPath: (config.outputPath as string) ?? "",
          privateKeyId: (config.privateKeyId as string) ?? "",
          verifySignature: (config.verifySignature as boolean) ?? false,
        }}
        onChange={(c) =>
          onChange({
            inputPath: c.inputPath,
            outputPath: c.outputPath,
            privateKeyId: c.privateKeyId,
            verifySignature: c.verifySignature,
          })
        }
      />
    );
  }

  if (typeKey === "pgp.sign") {
    return (
      <PgpSignForm
        config={{
          inputPath: (config.inputPath as string) ?? "",
          outputPath: (config.outputPath as string) ?? "",
          signingKeyId: (config.signingKeyId as string) ?? "",
          mode: (config.mode as string) ?? "detached",
          outputFormat: (config.outputFormat as string) ?? "binary",
        }}
        onChange={(c) =>
          onChange({
            inputPath: c.inputPath,
            outputPath: c.outputPath,
            signingKeyId: c.signingKeyId,
            mode: c.mode,
            outputFormat: c.outputFormat,
          })
        }
      />
    );
  }

  if (typeKey === "pgp.verify") {
    return (
      <PgpVerifyForm
        config={{
          inputPath: (config.inputPath as string) ?? "",
          signaturePath: (config.signaturePath as string) ?? "",
          signerKeyIds: (config.signerKeyIds as string[]) ?? [],
        }}
        onChange={(c) =>
          onChange({
            inputPath: c.inputPath,
            signaturePath: c.signaturePath,
            signerKeyIds: c.signerKeyIds,
          })
        }
      />
    );
  }

  // Default: file step config (file.copy, file.move)
  const fileConfig: FileStepConfig = {
    sourcePath: (config.sourcePath as string) ?? "",
    destinationPath: (config.destinationPath as string) ?? "",
    overwrite: (config.overwrite as boolean) ?? false,
  };
  return (
    <FileStepConfigForm
      config={fileConfig}
      onChange={(c) =>
        onChange({
          sourcePath: c.sourcePath,
          destinationPath: c.destinationPath,
          overwrite: c.overwrite,
        })
      }
    />
  );
}

export function parseStepConfig(configJson: string, typeKey?: string): StepConfig {
  if (typeKey === "azure_function.execute") {
    const az = parseAzureFunctionConfig(configJson);
    return {
      connectionId: az.connectionId,
      functionName: az.functionName,
      inputPayload: az.inputPayload,
      pollIntervalSec: az.pollIntervalSec,
      maxWaitSec: az.maxWaitSec,
      initialDelaySec: az.initialDelaySec,
    };
  }

  if (typeKey === "file.zip") {
    const zip = parseFileZipConfig(configJson);
    return {
      sourcePath: zip.sourcePath,
      outputPath: zip.outputPath,
      password: zip.password,
    };
  }

  if (typeKey === "file.unzip") {
    const unzip = parseFileUnzipConfig(configJson);
    return {
      archivePath: unzip.archivePath,
      outputDirectory: unzip.outputDirectory,
      password: unzip.password,
    };
  }

  if (typeKey === "file.delete") {
    const del = parseFileDeleteConfig(configJson);
    return {
      path: del.path,
      failIfNotFound: del.failIfNotFound,
    };
  }

  // Transfer steps
  if (typeKey && isTransferStep(typeKey)) {
    const op = operationOf(typeKey);

    if (op === "upload") {
      const c = parseTransferUploadConfig(configJson);
      return { connectionId: c.connectionId, localPath: c.localPath, remotePath: c.remotePath, atomicUpload: c.atomicUpload, atomicSuffix: c.atomicSuffix, resumePartial: c.resumePartial };
    }
    if (op === "download") {
      const c = parseTransferDownloadConfig(configJson);
      return { connectionId: c.connectionId, remotePath: c.remotePath, localPath: c.localPath, filePattern: c.filePattern, resumePartial: c.resumePartial, deleteAfterDownload: c.deleteAfterDownload };
    }
    if (op === "list") {
      const c = parseTransferListConfig(configJson);
      return { connectionId: c.connectionId, remotePath: c.remotePath, filePattern: c.filePattern };
    }
    if (op === "mkdir") {
      const c = parseTransferMkdirConfig(configJson);
      return { connectionId: c.connectionId, remotePath: c.remotePath };
    }
    if (op === "rmdir") {
      const c = parseTransferRmdirConfig(configJson);
      return { connectionId: c.connectionId, remotePath: c.remotePath, recursive: c.recursive };
    }
  }

  // PGP steps
  if (typeKey === "pgp.encrypt") {
    const c = parsePgpEncryptConfig(configJson);
    return { inputPath: c.inputPath, outputPath: c.outputPath, recipientKeyIds: c.recipientKeyIds, signingKeyId: c.signingKeyId, outputFormat: c.outputFormat };
  }
  if (typeKey === "pgp.decrypt") {
    const c = parsePgpDecryptConfig(configJson);
    return { inputPath: c.inputPath, outputPath: c.outputPath, privateKeyId: c.privateKeyId, verifySignature: c.verifySignature };
  }
  if (typeKey === "pgp.sign") {
    const c = parsePgpSignConfig(configJson);
    return { inputPath: c.inputPath, outputPath: c.outputPath, signingKeyId: c.signingKeyId, mode: c.mode, outputFormat: c.outputFormat };
  }
  if (typeKey === "pgp.verify") {
    const c = parsePgpVerifyConfig(configJson);
    return { inputPath: c.inputPath, signaturePath: c.signaturePath, signerKeyIds: c.signerKeyIds };
  }

  // Default: file copy/move step
  try {
    const parsed = JSON.parse(configJson);
    return {
      sourcePath: parsed.sourcePath || "",
      destinationPath: parsed.destinationPath || "",
      overwrite: parsed.overwrite || false,
    };
  } catch {
    return { sourcePath: "", destinationPath: "", overwrite: false };
  }
}

export function serializeStepConfig(config: StepConfig, typeKey?: string): string {
  if (typeKey === "azure_function.execute") {
    return serializeAzureFunctionConfig({
      connectionId: (config.connectionId as string) ?? "",
      functionName: (config.functionName as string) ?? "",
      inputPayload: (config.inputPayload as string) ?? "",
      pollIntervalSec: (config.pollIntervalSec as number) ?? 15,
      maxWaitSec: (config.maxWaitSec as number) ?? 3600,
      initialDelaySec: (config.initialDelaySec as number) ?? 30,
    });
  }

  if (typeKey === "file.zip") {
    return serializeFileZipConfig({
      sourcePath: (config.sourcePath as string) ?? "",
      outputPath: (config.outputPath as string) ?? "",
      password: (config.password as string) ?? "",
    });
  }

  if (typeKey === "file.unzip") {
    return serializeFileUnzipConfig({
      archivePath: (config.archivePath as string) ?? "",
      outputDirectory: (config.outputDirectory as string) ?? "",
      password: (config.password as string) ?? "",
    });
  }

  if (typeKey === "file.delete") {
    return serializeFileDeleteConfig({
      path: (config.path as string) ?? "",
      failIfNotFound: (config.failIfNotFound as boolean) ?? false,
    });
  }

  // Transfer steps
  if (typeKey && isTransferStep(typeKey)) {
    const op = operationOf(typeKey);

    if (op === "upload") {
      return serializeTransferUploadConfig({
        connectionId: (config.connectionId as string) ?? "",
        localPath: (config.localPath as string) ?? "",
        remotePath: (config.remotePath as string) ?? "",
        atomicUpload: (config.atomicUpload as boolean) ?? false,
        atomicSuffix: (config.atomicSuffix as string) ?? ".tmp",
        resumePartial: (config.resumePartial as boolean) ?? false,
      });
    }
    if (op === "download") {
      return serializeTransferDownloadConfig({
        connectionId: (config.connectionId as string) ?? "",
        remotePath: (config.remotePath as string) ?? "",
        localPath: (config.localPath as string) ?? "",
        filePattern: (config.filePattern as string) ?? "",
        resumePartial: (config.resumePartial as boolean) ?? false,
        deleteAfterDownload: (config.deleteAfterDownload as boolean) ?? false,
      });
    }
    if (op === "list") {
      return serializeTransferListConfig({
        connectionId: (config.connectionId as string) ?? "",
        remotePath: (config.remotePath as string) ?? "",
        filePattern: (config.filePattern as string) ?? "",
      });
    }
    if (op === "mkdir") {
      return serializeTransferMkdirConfig({
        connectionId: (config.connectionId as string) ?? "",
        remotePath: (config.remotePath as string) ?? "",
      });
    }
    if (op === "rmdir") {
      return serializeTransferRmdirConfig({
        connectionId: (config.connectionId as string) ?? "",
        remotePath: (config.remotePath as string) ?? "",
        recursive: (config.recursive as boolean) ?? false,
      });
    }
  }

  // PGP steps
  if (typeKey === "pgp.encrypt") {
    return serializePgpEncryptConfig({
      inputPath: (config.inputPath as string) ?? "",
      outputPath: (config.outputPath as string) ?? "",
      recipientKeyIds: (config.recipientKeyIds as string[]) ?? [],
      signingKeyId: (config.signingKeyId as string) ?? "",
      outputFormat: (config.outputFormat as string) ?? "binary",
    });
  }
  if (typeKey === "pgp.decrypt") {
    return serializePgpDecryptConfig({
      inputPath: (config.inputPath as string) ?? "",
      outputPath: (config.outputPath as string) ?? "",
      privateKeyId: (config.privateKeyId as string) ?? "",
      verifySignature: (config.verifySignature as boolean) ?? false,
    });
  }
  if (typeKey === "pgp.sign") {
    return serializePgpSignConfig({
      inputPath: (config.inputPath as string) ?? "",
      outputPath: (config.outputPath as string) ?? "",
      signingKeyId: (config.signingKeyId as string) ?? "",
      mode: (config.mode as string) ?? "detached",
      outputFormat: (config.outputFormat as string) ?? "binary",
    });
  }
  if (typeKey === "pgp.verify") {
    return serializePgpVerifyConfig({
      inputPath: (config.inputPath as string) ?? "",
      signaturePath: (config.signaturePath as string) ?? "",
      signerKeyIds: (config.signerKeyIds as string[]) ?? [],
    });
  }

  // Default: file copy/move step
  return JSON.stringify({
    sourcePath: config.sourcePath,
    destinationPath: config.destinationPath,
    overwrite: config.overwrite,
  });
}
