"use client";

import { useState, useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  useAuthProvider,
  useUpdateAuthProvider,
  useTestAuthProvider,
} from "@/lib/hooks/use-auth-providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";
import type { RoleMappingRule } from "@/lib/types";
import { Trash2, PlusCircle, CheckCircle2, XCircle, Loader2 } from "lucide-react";

const selectClass =
  "flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring";

const NAME_ID_FORMATS = [
  { value: "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified", label: "Unspecified" },
  { value: "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress", label: "Email Address" },
  { value: "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent", label: "Persistent" },
  { value: "urn:oasis:names:tc:SAML:2.0:nameid-format:transient", label: "Transient" },
];

function getString(config: Record<string, unknown>, key: string): string {
  const val = config[key];
  return typeof val === "string" ? val : "";
}

function getBool(config: Record<string, unknown>, key: string): boolean {
  return config[key] === true;
}

function getScopes(config: Record<string, unknown>): string {
  const val = config["scopes"];
  if (Array.isArray(val)) return val.join(",");
  if (typeof val === "string") return val;
  return "openid,profile,email";
}

export default function EditAuthProviderPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;
  const { data, isLoading } = useAuthProvider(id);
  const updateAuthProvider = useUpdateAuthProvider();
  const testAuthProvider = useTestAuthProvider();

  const provider = data?.data;

  // General
  const [name, setName] = useState("");
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

  // Test result
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    if (provider && !initialized) {
      const cfg = provider.configuration;
      setName(provider.name);
      setIsEnabled(provider.isEnabled);
      setDisplayOrder(provider.displayOrder);
      setIconUrl(provider.iconUrl ?? "");
      setAutoProvision(provider.autoProvision);
      setDefaultRole(provider.defaultRole);
      setAllowLocalPassword(provider.allowLocalPassword);
      setRoleMappingEnabled(provider.roleMapping?.enabled ?? false);
      setRoleMappingRules(provider.roleMapping?.rules ?? []);

      if (provider.type === "oidc") {
        setAuthorityUrl(getString(cfg, "authority_url"));
        setClientId(getString(cfg, "client_id"));
        // Don't pre-fill secret — show placeholder only
        setScopes(getScopes(cfg));
      } else {
        setEntityId(getString(cfg, "entity_id"));
        setSsoUrl(getString(cfg, "sso_url"));
        setCertificate(getString(cfg, "certificate"));
        setSignAuthnRequests(getBool(cfg, "sign_authn_requests"));
        setNameIdFormat(getString(cfg, "name_id_format") || NAME_ID_FORMATS[0].value);
      }

      setInitialized(true);
    }
  }, [provider, initialized]);

  if (isLoading) return <div className="text-sm text-muted-foreground">Loading provider...</div>;
  if (!provider) return <div className="text-sm text-muted-foreground">Auth provider not found.</div>;

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

  async function handleUpdate(e: React.FormEvent) {
    e.preventDefault();
    setTestResult(null);
    if (!provider) return;

    const configuration: Record<string, unknown> =
      provider.type === "oidc"
        ? {
            authority_url: authorityUrl,
            client_id: clientId,
            ...(clientSecret ? { client_secret: clientSecret } : {}),
            scopes: scopes
              .split(",")
              .map((s) => s.trim())
              .filter(Boolean),
          }
        : {
            entity_id: entityId,
            sso_url: ssoUrl,
            ...(certificate ? { certificate } : {}),
            sign_authn_requests: signAuthnRequests,
            name_id_format: nameIdFormat,
          };

    try {
      await updateAuthProvider.mutateAsync({
        id,
        data: {
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
        },
      });
      toast.success("Auth provider updated successfully.");
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to update auth provider.");
    }
  }

  async function handleTest() {
    setTestResult(null);
    try {
      const result = await testAuthProvider.mutateAsync(id);
      if (result.data) {
        setTestResult({ success: result.data.success, message: result.data.message });
      }
    } catch (err) {
      if (err instanceof ApiClientError) {
        setTestResult({ success: false, message: err.message });
      } else {
        setTestResult({ success: false, message: "Test failed." });
      }
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{provider.name}</h1>
          <p className="text-sm text-muted-foreground">
            {provider.type.toUpperCase()} provider · Created{" "}
            {new Date(provider.createdAt).toLocaleDateString()}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={handleTest}
            disabled={testAuthProvider.isPending}
          >
            {testAuthProvider.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Test Connection
          </Button>
        </div>
      </div>

      {testResult && (
        <div
          className={`flex items-center gap-2 rounded-md p-3 text-sm ${
            testResult.success
              ? "bg-green-50 text-green-800 dark:bg-green-900/20 dark:text-green-400"
              : "bg-destructive/10 text-destructive"
          }`}
        >
          {testResult.success ? (
            <CheckCircle2 className="h-4 w-4 shrink-0" />
          ) : (
            <XCircle className="h-4 w-4 shrink-0" />
          )}
          {testResult.message}
        </div>
      )}

      <form onSubmit={handleUpdate} className="max-w-2xl space-y-8">
        {/* General */}
        <section className="space-y-4">
          <h2 className="text-lg font-medium">General</h2>

          <div className="space-y-2">
            <Label htmlFor="name">Name <span className="text-destructive">*</span></Label>
            <Input
              id="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
            />
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

          {provider.type === "oidc" ? (
            <>
              <div className="space-y-2">
                <Label htmlFor="authorityUrl">Authority URL <span className="text-destructive">*</span></Label>
                <Input
                  id="authorityUrl"
                  type="url"
                  value={authorityUrl}
                  onChange={(e) => setAuthorityUrl(e.target.value)}
                  required
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="clientId">Client ID <span className="text-destructive">*</span></Label>
                <Input
                  id="clientId"
                  value={clientId}
                  onChange={(e) => setClientId(e.target.value)}
                  required
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="clientSecret">
                  Client Secret <span className="text-muted-foreground">(leave blank to keep existing)</span>
                </Label>
                <Input
                  id="clientSecret"
                  type="password"
                  value={clientSecret}
                  onChange={(e) => setClientSecret(e.target.value)}
                  placeholder="••••••••"
                  autoComplete="new-password"
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="scopes">Scopes</Label>
                <Input
                  id="scopes"
                  value={scopes}
                  onChange={(e) => setScopes(e.target.value)}
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
          <Button type="submit" disabled={updateAuthProvider.isPending}>
            {updateAuthProvider.isPending ? "Saving..." : "Save Changes"}
          </Button>
          <Button
            type="button"
            variant="outline"
            onClick={() => router.push("/settings/auth-providers")}
          >
            Back
          </Button>
        </div>
      </form>
    </div>
  );
}
