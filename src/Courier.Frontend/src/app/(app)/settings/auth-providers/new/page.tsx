"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useCreateAuthProvider } from "@/lib/hooks/use-auth-providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";
import type { RoleMappingRule } from "@/lib/types";
import { Trash2, PlusCircle } from "lucide-react";

const selectClass =
  "flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring";

const NAME_ID_FORMATS = [
  { value: "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified", label: "Unspecified" },
  { value: "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress", label: "Email Address" },
  { value: "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent", label: "Persistent" },
  { value: "urn:oasis:names:tc:SAML:2.0:nameid-format:transient", label: "Transient" },
];

export default function NewAuthProviderPage() {
  const router = useRouter();
  const createAuthProvider = useCreateAuthProvider();

  // General
  const [name, setName] = useState("");
  const [type, setType] = useState<"oidc" | "saml">("oidc");
  const [isEnabled, setIsEnabled] = useState(true);
  const [displayOrder, setDisplayOrder] = useState(0);
  const [iconUrl, setIconUrl] = useState("");

  // OIDC config
  const [authorityUrl, setAuthorityUrl] = useState("");
  const [clientId, setClientId] = useState("");
  const [clientSecret, setClientSecret] = useState("");
  const [scopes, setScopes] = useState("openid,profile,email");

  // SAML config
  const [entityId, setEntityId] = useState("");
  const [ssoUrl, setSsoUrl] = useState("");
  const [certificate, setCertificate] = useState("");
  const [signAuthnRequests, setSignAuthnRequests] = useState(false);
  const [nameIdFormat, setNameIdFormat] = useState(NAME_ID_FORMATS[0].value);

  // Provisioning
  const [autoProvision, setAutoProvision] = useState(true);
  const [defaultRole, setDefaultRole] = useState("viewer");
  const [allowLocalPassword, setAllowLocalPassword] = useState(false);

  // Role mapping
  const [roleMappingEnabled, setRoleMappingEnabled] = useState(false);
  const [roleMappingRules, setRoleMappingRules] = useState<RoleMappingRule[]>([]);

  function addRule() {
    setRoleMappingRules([...roleMappingRules, { claim: "", value: "", role: "viewer" }]);
  }

  function removeRule(index: number) {
    setRoleMappingRules(roleMappingRules.filter((_, i) => i !== index));
  }

  function updateRule(index: number, field: keyof RoleMappingRule, value: string) {
    setRoleMappingRules(
      roleMappingRules.map((r, i) => (i === index ? { ...r, [field]: value } : r))
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const configuration: Record<string, unknown> =
      type === "oidc"
        ? {
            authority_url: authorityUrl,
            client_id: clientId,
            client_secret: clientSecret || undefined,
            scopes: scopes
              .split(",")
              .map((s) => s.trim())
              .filter(Boolean),
          }
        : {
            entity_id: entityId,
            sso_url: ssoUrl,
            certificate: certificate || undefined,
            sign_authn_requests: signAuthnRequests,
            name_id_format: nameIdFormat,
          };

    try {
      await createAuthProvider.mutateAsync({
        type,
        name,
        isEnabled,
        displayOrder,
        iconUrl: iconUrl || undefined,
        configuration,
        autoProvision,
        defaultRole,
        allowLocalPassword,
        roleMapping: roleMappingEnabled
          ? { enabled: true, rules: roleMappingRules }
          : { enabled: false, rules: [] },
      });
      toast.success("Auth provider created successfully.");
      router.push("/settings/auth-providers");
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to create auth provider.");
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Add Auth Provider</h1>
        <p className="text-sm text-muted-foreground">Configure a new SSO or external authentication provider.</p>
      </div>

      <form onSubmit={handleSubmit} className="max-w-2xl space-y-8">
        {/* General */}
        <section className="space-y-4">
          <h2 className="text-lg font-medium">General</h2>

          <div className="space-y-2">
            <Label htmlFor="name">Name <span className="text-destructive">*</span></Label>
            <Input
              id="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Company Azure AD"
              required
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="type">Type <span className="text-destructive">*</span></Label>
            <select
              id="type"
              className={selectClass}
              value={type}
              onChange={(e) => setType(e.target.value as "oidc" | "saml")}
              required
            >
              <option value="oidc">OIDC (OpenID Connect)</option>
              <option value="saml">SAML 2.0</option>
            </select>
          </div>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isEnabled"
              checked={isEnabled}
              onChange={(e) => setIsEnabled(e.target.checked)}
              className="h-4 w-4 rounded border-input"
            />
            <Label htmlFor="isEnabled">Enabled</Label>
          </div>

          <div className="space-y-2">
            <Label htmlFor="displayOrder">Display Order</Label>
            <Input
              id="displayOrder"
              type="number"
              min={0}
              value={displayOrder}
              onChange={(e) => setDisplayOrder(parseInt(e.target.value) || 0)}
              className="max-w-xs"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="iconUrl">
              Icon URL <span className="text-muted-foreground">(optional)</span>
            </Label>
            <Input
              id="iconUrl"
              type="url"
              value={iconUrl}
              onChange={(e) => setIconUrl(e.target.value)}
              placeholder="https://example.com/icon.png"
            />
          </div>
        </section>

        {/* Configuration */}
        <section className="space-y-4 border-t pt-6">
          <h2 className="text-lg font-medium">Configuration</h2>

          {type === "oidc" ? (
            <>
              <div className="space-y-2">
                <Label htmlFor="authorityUrl">Authority URL <span className="text-destructive">*</span></Label>
                <Input
                  id="authorityUrl"
                  type="url"
                  value={authorityUrl}
                  onChange={(e) => setAuthorityUrl(e.target.value)}
                  placeholder="https://login.microsoftonline.com/{tenant}/v2.0"
                  required
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="clientId">Client ID <span className="text-destructive">*</span></Label>
                <Input
                  id="clientId"
                  value={clientId}
                  onChange={(e) => setClientId(e.target.value)}
                  placeholder="Application (client) ID"
                  required
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="clientSecret">
                  Client Secret <span className="text-muted-foreground">(optional)</span>
                </Label>
                <Input
                  id="clientSecret"
                  type="password"
                  value={clientSecret}
                  onChange={(e) => setClientSecret(e.target.value)}
                  autoComplete="new-password"
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="scopes">Scopes</Label>
                <Input
                  id="scopes"
                  value={scopes}
                  onChange={(e) => setScopes(e.target.value)}
                  placeholder="openid,profile,email"
                />
                <p className="text-xs text-muted-foreground">Comma-separated list of OAuth scopes.</p>
              </div>
            </>
          ) : (
            <>
              <div className="space-y-2">
                <Label htmlFor="entityId">Entity ID <span className="text-destructive">*</span></Label>
                <Input
                  id="entityId"
                  value={entityId}
                  onChange={(e) => setEntityId(e.target.value)}
                  placeholder="https://your-app.example.com"
                  required
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="ssoUrl">SSO URL <span className="text-destructive">*</span></Label>
                <Input
                  id="ssoUrl"
                  type="url"
                  value={ssoUrl}
                  onChange={(e) => setSsoUrl(e.target.value)}
                  placeholder="https://idp.example.com/sso"
                  required
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="certificate">
                  Certificate <span className="text-muted-foreground">(base64 X.509, optional)</span>
                </Label>
                <textarea
                  id="certificate"
                  className="flex min-h-[100px] w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                  value={certificate}
                  onChange={(e) => setCertificate(e.target.value)}
                  placeholder="MIIC..."
                />
              </div>

              <div className="flex items-center gap-2">
                <input
                  type="checkbox"
                  id="signAuthnRequests"
                  checked={signAuthnRequests}
                  onChange={(e) => setSignAuthnRequests(e.target.checked)}
                  className="h-4 w-4 rounded border-input"
                />
                <Label htmlFor="signAuthnRequests">Sign AuthnRequests</Label>
              </div>

              <div className="space-y-2">
                <Label htmlFor="nameIdFormat">NameID Format</Label>
                <select
                  id="nameIdFormat"
                  className={selectClass}
                  value={nameIdFormat}
                  onChange={(e) => setNameIdFormat(e.target.value)}
                >
                  {NAME_ID_FORMATS.map((f) => (
                    <option key={f.value} value={f.value}>
                      {f.label}
                    </option>
                  ))}
                </select>
              </div>
            </>
          )}
        </section>

        {/* User Provisioning */}
        <section className="space-y-4 border-t pt-6">
          <h2 className="text-lg font-medium">User Provisioning</h2>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="autoProvision"
              checked={autoProvision}
              onChange={(e) => setAutoProvision(e.target.checked)}
              className="h-4 w-4 rounded border-input"
            />
            <Label htmlFor="autoProvision">Auto-provision users on first login</Label>
          </div>

          <div className="space-y-2">
            <Label htmlFor="defaultRole">Default Role</Label>
            <select
              id="defaultRole"
              className={selectClass}
              value={defaultRole}
              onChange={(e) => setDefaultRole(e.target.value)}
            >
              <option value="admin">Admin</option>
              <option value="operator">Operator</option>
              <option value="viewer">Viewer</option>
            </select>
          </div>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="allowLocalPassword"
              checked={allowLocalPassword}
              onChange={(e) => setAllowLocalPassword(e.target.checked)}
              className="h-4 w-4 rounded border-input"
            />
            <Label htmlFor="allowLocalPassword">Allow local password login for SSO users</Label>
          </div>
        </section>

        {/* Role Mapping */}
        <section className="space-y-4 border-t pt-6">
          <h2 className="text-lg font-medium">Role Mapping</h2>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="roleMappingEnabled"
              checked={roleMappingEnabled}
              onChange={(e) => setRoleMappingEnabled(e.target.checked)}
              className="h-4 w-4 rounded border-input"
            />
            <Label htmlFor="roleMappingEnabled">Enable claim-based role mapping</Label>
          </div>

          {roleMappingEnabled && (
            <div className="space-y-3">
              {roleMappingRules.length > 0 && (
                <div className="rounded-md border">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b bg-muted/50">
                        <th className="px-3 py-2 text-left font-medium">Claim</th>
                        <th className="px-3 py-2 text-left font-medium">Value</th>
                        <th className="px-3 py-2 text-left font-medium">Role</th>
                        <th className="px-3 py-2 text-right font-medium"></th>
                      </tr>
                    </thead>
                    <tbody>
                      {roleMappingRules.map((rule, i) => (
                        <tr key={i} className="border-b last:border-0">
                          <td className="px-3 py-2">
                            <Input
                              value={rule.claim}
                              onChange={(e) => updateRule(i, "claim", e.target.value)}
                              placeholder="e.g. groups"
                            />
                          </td>
                          <td className="px-3 py-2">
                            <Input
                              value={rule.value}
                              onChange={(e) => updateRule(i, "value", e.target.value)}
                              placeholder="e.g. courier-admins"
                            />
                          </td>
                          <td className="px-3 py-2">
                            <select
                              className={selectClass}
                              value={rule.role}
                              onChange={(e) => updateRule(i, "role", e.target.value)}
                            >
                              <option value="admin">Admin</option>
                              <option value="operator">Operator</option>
                              <option value="viewer">Viewer</option>
                            </select>
                          </td>
                          <td className="px-3 py-2 text-right">
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon"
                              className="h-8 w-8 text-destructive hover:text-destructive"
                              onClick={() => removeRule(i)}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              <Button type="button" variant="outline" size="sm" onClick={addRule}>
                <PlusCircle className="mr-2 h-4 w-4" />
                Add Rule
              </Button>
            </div>
          )}
        </section>

        <div className="flex gap-3 border-t pt-6">
          <Button type="submit" disabled={createAuthProvider.isPending}>
            {createAuthProvider.isPending ? "Creating..." : "Create Provider"}
          </Button>
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push("/settings/auth-providers")}
          >
            Cancel
          </Button>
        </div>
      </form>
    </div>
  );
}
