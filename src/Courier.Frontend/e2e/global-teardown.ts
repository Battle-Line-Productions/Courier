import type { FullConfig } from "@playwright/test";
import { TEST_ADMIN } from "./global-setup";

const API_BASE_URL = process.env.API_URL || "http://localhost:5000";

async function getToken(): Promise<string | null> {
  try {
    const resp = await fetch(`${API_BASE_URL}/api/v1/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        username: TEST_ADMIN.username,
        password: TEST_ADMIN.password,
      }),
    });
    const body = await resp.json();
    return body.data?.accessToken ?? null;
  } catch {
    return null;
  }
}

async function cleanupEntities(
  token: string,
  endpoint: string,
  searchField: string,
  nameField: string,
  prefix: string
): Promise<number> {
  let deleted = 0;
  const headers = {
    Authorization: `Bearer ${token}`,
    "Content-Type": "application/json",
  };

  // Fetch all pages of matching entities
  let page = 1;
  while (true) {
    const url = `${API_BASE_URL}/api/v1/${endpoint}?${searchField}=${encodeURIComponent(prefix)}&page=${page}&pageSize=100`;
    const resp = await fetch(url, { headers });
    if (!resp.ok) break;
    const body = await resp.json();
    const items = body.data ?? [];
    if (items.length === 0) break;

    // Delete matching items in parallel
    const toDelete = items.filter(
      (item: Record<string, string>) =>
        item[nameField]?.startsWith(prefix)
    );
    await Promise.allSettled(
      toDelete.map((item: { id: string }) =>
        fetch(`${API_BASE_URL}/api/v1/${endpoint}/${item.id}`, {
          method: "DELETE",
          headers,
        })
      )
    );
    deleted += toDelete.length;

    if (items.length < 100) break;
    page++;
  }
  return deleted;
}

async function globalTeardown(_config: FullConfig): Promise<void> {
  const token = await getToken();
  if (!token) {
    console.log("[teardown] Could not authenticate — skipping cleanup.");
    return;
  }

  // Also unlock the test admin account in case it got locked during the run
  // (this is a no-op if not locked, but prevents cascading failures on next run)
  try {
    await fetch(`${API_BASE_URL}/api/v1/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        username: TEST_ADMIN.username,
        password: TEST_ADMIN.password,
      }),
    });
  } catch {
    // Best effort
  }

  const cleanups: Array<[string, string, string]> = [
    // [endpoint, searchParam, nameField]
    ["notification-rules", "search", "name"],
    ["monitors", "search", "name"],
    ["tags", "search", "name"],
    ["chains", "search", "name"],
    ["connections", "search", "name"],
    ["pgp-keys", "search", "name"],
    ["ssh-keys", "search", "name"],
    ["jobs", "search", "name"],
    ["users", "search", "username"],
  ];

  let totalDeleted = 0;
  for (const [endpoint, searchField, nameField] of cleanups) {
    const prefix = endpoint === "users" ? "e2e-" : "e2e-";
    const count = await cleanupEntities(
      token,
      endpoint,
      searchField,
      nameField,
      prefix
    );
    totalDeleted += count;
  }

  if (totalDeleted > 0) {
    console.log(
      `[teardown] Cleaned up ${totalDeleted} orphaned e2e entities.`
    );
  }
}

export default globalTeardown;
