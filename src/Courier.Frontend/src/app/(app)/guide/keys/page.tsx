"use client";

import { GuideImage } from "@/components/guide/guide-image";
import { GuidePrevNext } from "@/components/guide/guide-nav";

export default function KeysGuide() {
  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Keys</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Courier manages two types of cryptographic keys: PGP keys for file encryption
          and decryption, and SSH keys for secure server authentication.
        </p>
      </div>

      {/* PGP Keys */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">PGP Keys</h2>
        <p className="text-sm text-muted-foreground">
          PGP keys are used by the <code className="rounded bg-muted px-1 py-0.5 text-xs">pgp.encrypt</code>{" "}
          and <code className="rounded bg-muted px-1 py-0.5 text-xs">pgp.decrypt</code> job
          steps. You can generate new key pairs directly in Courier or import existing
          public keys from your partners.
        </p>
        <GuideImage
          src="/guide/screenshots/keys-pgp.png"
          alt="PGP Keys tab"
          caption="The PGP Keys tab showing managed encryption keys"
        />
      </section>

      {/* Generating a PGP Key */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Generating a PGP Key</h2>
        <p className="text-sm text-muted-foreground">
          Click <strong>Generate Key</strong> to create a new PGP key pair. You&apos;ll
          need to provide:
        </p>
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li><strong>Name</strong> — A descriptive label for the key</li>
          <li><strong>Algorithm</strong> — The encryption algorithm (ECC Curve25519 recommended)</li>
          <li><strong>Purpose</strong> — Whether the key is for encryption, signing, or both</li>
          <li><strong>Real Name &amp; Email</strong> — Identity information embedded in the key</li>
        </ul>
        <GuideImage
          src="/guide/screenshots/pgp-key-generate.png"
          alt="Generate PGP Key form"
          caption="Generate a new PGP key pair with the desired algorithm and purpose"
        />
      </section>

      {/* PGP Key Detail */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">PGP Key Details</h2>
        <p className="text-sm text-muted-foreground">
          Click a PGP key to view its details, including the fingerprint, algorithm, and
          creation date. You can also export the public key to share with partners, or
          manage the key lifecycle (activate, retire, revoke).
        </p>
        <GuideImage
          src="/guide/screenshots/pgp-key-detail.png"
          alt="PGP Key detail page"
          caption="Key details with fingerprint, export, and lifecycle management"
        />
        <h3 className="text-base font-medium">Key Lifecycle</h3>
        <ul className="list-inside list-disc space-y-1 text-sm text-muted-foreground">
          <li><strong>Active</strong> — The key is available for use in job steps</li>
          <li><strong>Retired</strong> — The key cannot be used in new jobs but existing references remain</li>
          <li><strong>Revoked</strong> — The key is permanently disabled and cannot be reactivated</li>
        </ul>
      </section>

      {/* SSH Keys */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">SSH Keys</h2>
        <p className="text-sm text-muted-foreground">
          SSH keys are used for authenticating SFTP connections. Instead of using a
          password, you can assign an SSH key to a connection for more secure
          public-key authentication.
        </p>
        <GuideImage
          src="/guide/screenshots/keys-ssh.png"
          alt="SSH Keys tab"
          caption="The SSH Keys tab for managing authentication keys"
        />
      </section>

      {/* Generating an SSH Key */}
      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Generating an SSH Key</h2>
        <p className="text-sm text-muted-foreground">
          Click <strong>Generate Key</strong> on the SSH tab to create a new key pair.
          Select the key type — Ed25519 is recommended for its speed and security.
        </p>
        <GuideImage
          src="/guide/screenshots/ssh-key-generate.png"
          alt="Generate SSH Key form"
          caption="Generate a new SSH key pair for SFTP authentication"
        />
        <div className="rounded-lg border-l-4 border-primary/60 bg-primary/5 p-4">
          <p className="text-sm">
            <strong>After generating:</strong> Copy the public key from the key detail
            page and add it to the remote server&apos;s{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-xs">~/.ssh/authorized_keys</code>{" "}
            file. Then update your SFTP connection to use &quot;SSH Key&quot; authentication
            and select this key.
          </p>
        </div>
      </section>

      <GuidePrevNext
        prev={{ label: "Connections", href: "/guide/connections" }}
        next={{ label: "Chains", href: "/guide/chains" }}
      />
    </div>
  );
}
