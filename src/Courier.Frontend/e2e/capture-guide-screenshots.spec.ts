/**
 * Playwright script to capture screenshots for the User Guide.
 *
 * Run with:
 *   API_URL=http://localhost:60606 FRONTEND_URL=http://localhost:55674 \
 *     npx playwright test e2e/capture-guide-screenshots.spec.ts --project=chromium --workers=1
 *
 * Screenshots are saved to public/guide/screenshots/
 */
import { test, expect } from "@playwright/test";
import path from "path";

const SCREENSHOT_DIR = path.resolve(__dirname, "../public/guide/screenshots");

const API_URL = process.env.API_URL || "http://localhost:5000";
const CREDENTIALS = {
  username: process.env.GUIDE_USER || "admin",
  password: process.env.GUIDE_PASS || "!Cc20080754",
};

// ── Helpers ──

async function apiLogin(request: any): Promise<string> {
  const res = await request.post(`${API_URL}/api/v1/auth/login`, {
    data: CREDENTIALS,
  });
  const body = await res.json();
  return body.data.accessToken;
}

function authHeaders(token: string) {
  return { Authorization: `Bearer ${token}`, "Content-Type": "application/json" };
}

async function screenshot(page: any, name: string) {
  await page.screenshot({
    path: path.join(SCREENSHOT_DIR, `${name}.png`),
    fullPage: false,
  });
}

async function authenticatePage(page: any) {
  // Use actual UI login for reliability
  await page.goto("/login");
  await page.waitForLoadState("networkidle");

  // Fill in credentials
  await page.getByLabel("Username").fill(CREDENTIALS.username);
  await page.getByLabel("Password").fill(CREDENTIALS.password);
  await page.getByRole("button", { name: "Sign In" }).click();

  // Wait for redirect to dashboard
  await page.waitForURL("/", { timeout: 15_000 });
  await page.waitForLoadState("networkidle");
  // Wait for page content to fully render
  await page.waitForTimeout(2000);
}

// ── Seed Data ──

interface SeedData {
  jobs: any[];
  connections: any[];
  pgpKeys: any[];
  sshKeys: any[];
  tags: any[];
  chains: any[];
  monitors: any[];
  notifications: any[];
}

async function seedData(request: any): Promise<SeedData> {
  const token = await apiLogin(request);
  const h = authHeaders(token);
  const seed: SeedData = {
    jobs: [],
    connections: [],
    pgpKeys: [],
    sshKeys: [],
    tags: [],
    chains: [],
    monitors: [],
    notifications: [],
  };

  // Create tags
  for (const t of [
    { name: "guide-production", color: "#ef4444", category: "environment" },
    { name: "guide-staging", color: "#f59e0b", category: "environment" },
    { name: "guide-daily", color: "#3b82f6", category: "schedule" },
  ]) {
    const res = await request.post(`${API_URL}/api/v1/tags`, { headers: h, data: t });
    if (res.ok()) seed.tags.push((await res.json()).data);
  }

  // Create connections
  for (const c of [
    { name: "guide-sftp-server", protocol: "sftp", host: "sftp.example.com", port: 22, authMethod: "password", username: "courier-user", password: "secret123" },
    { name: "guide-ftp-backup", protocol: "ftp", host: "ftp.backup.local", port: 21, authMethod: "password", username: "backupuser", password: "secret456" },
  ]) {
    const res = await request.post(`${API_URL}/api/v1/connections`, { headers: h, data: c });
    if (res.ok()) seed.connections.push((await res.json()).data);
  }

  // Create PGP key
  const pgpRes = await request.post(`${API_URL}/api/v1/pgp-keys/generate`, {
    headers: h,
    data: { name: "guide-encryption-key", algorithm: "ecc_curve25519", purpose: "encryption", realName: "Courier Guide", email: "guide@courier.example" },
  });
  if (pgpRes.ok()) seed.pgpKeys.push((await pgpRes.json()).data);

  // Create SSH key
  const sshRes = await request.post(`${API_URL}/api/v1/ssh-keys/generate`, {
    headers: h,
    data: { name: "guide-ssh-key", keyType: "ed25519" },
  });
  if (sshRes.ok()) seed.sshKeys.push((await sshRes.json()).data);

  // Create jobs with steps
  for (const j of [
    { name: "guide-daily-report-transfer", description: "Downloads daily reports from SFTP and archives locally" },
    { name: "guide-encrypted-backup", description: "Encrypts and uploads backup files to remote server" },
    { name: "guide-data-sync", description: "Syncs data files between environments" },
  ]) {
    const res = await request.post(`${API_URL}/api/v1/jobs`, { headers: h, data: j });
    if (res.ok()) seed.jobs.push((await res.json()).data);
  }

  // Add steps to first job
  if (seed.jobs[0]) {
    await request.put(`${API_URL}/api/v1/jobs/${seed.jobs[0].id}/steps`, {
      headers: h,
      data: {
        steps: [
          { name: "Download Report", typeKey: "file.copy", stepOrder: 1, configuration: JSON.stringify({ source_path: "/reports/daily/*.csv", destination_path: "/local/incoming/" }), timeoutSeconds: 300 },
          { name: "Archive Files", typeKey: "file.move", stepOrder: 2, configuration: JSON.stringify({ source_path: "/local/incoming/*.csv", destination_path: "/local/archive/" }), timeoutSeconds: 120 },
        ],
      },
    });
  }

  // Add steps to second job
  if (seed.jobs[1]) {
    await request.put(`${API_URL}/api/v1/jobs/${seed.jobs[1].id}/steps`, {
      headers: h,
      data: {
        steps: [
          { name: "Encrypt Backup", typeKey: "pgp.encrypt", stepOrder: 1, configuration: JSON.stringify({ source_path: "/backups/latest.tar.gz" }), timeoutSeconds: 600 },
          { name: "Upload to SFTP", typeKey: "sftp.upload", stepOrder: 2, configuration: JSON.stringify({ destination_path: "/remote/backups/" }), timeoutSeconds: 300 },
        ],
      },
    });
  }

  // Trigger first job so we have execution history
  if (seed.jobs[0]) {
    await request.post(`${API_URL}/api/v1/jobs/${seed.jobs[0].id}/trigger`, {
      headers: h,
      data: { triggeredBy: "guide-screenshot" },
    });
  }

  // Assign tags to jobs
  if (seed.tags[0] && seed.jobs[0]) {
    await request.post(`${API_URL}/api/v1/tags/assign`, {
      headers: h,
      data: { assignments: [{ tagId: seed.tags[0].id, entityType: "job", entityId: seed.jobs[0].id }] },
    });
  }

  // Create chains
  const chainRes = await request.post(`${API_URL}/api/v1/chains`, {
    headers: h,
    data: { name: "guide-nightly-pipeline", description: "Runs report transfer then encrypted backup sequentially" },
  });
  if (chainRes.ok()) {
    const chain = (await chainRes.json()).data;
    seed.chains.push(chain);

    // Add members if we have jobs
    if (seed.jobs.length >= 2) {
      await request.put(`${API_URL}/api/v1/chains/${chain.id}/members`, {
        headers: h,
        data: {
          members: [
            { jobId: seed.jobs[0].id, executionOrder: 1 },
            { jobId: seed.jobs[1].id, executionOrder: 2, dependsOnMemberIndex: 0 },
          ],
        },
      });
    }
  }

  // Create monitors
  const monRes = await request.post(`${API_URL}/api/v1/monitors`, {
    headers: h,
    data: {
      name: "guide-incoming-watcher",
      watchTarget: JSON.stringify({ type: "local", path: "/data/incoming" }),
      triggerEvents: 1,
      pollingIntervalSec: 60,
      jobIds: seed.jobs.length > 0 ? [seed.jobs[0].id] : [],
    },
  });
  if (monRes.ok()) seed.monitors.push((await monRes.json()).data);

  // Create notification rules
  const notifRes = await request.post(`${API_URL}/api/v1/notification-rules`, {
    headers: h,
    data: {
      name: "guide-failure-alert",
      entityType: "job",
      eventTypes: ["job_failed"],
      channel: "email",
      channelConfig: { recipients: ["ops-team@example.com"], subjectPrefix: "[Courier Alert]" },
      isEnabled: true,
    },
  });
  if (notifRes.ok()) seed.notifications.push((await notifRes.json()).data);

  return seed;
}

async function cleanupData(request: any, seed: SeedData) {
  const token = await apiLogin(request);
  const h = authHeaders(token);

  for (const n of seed.notifications) await request.delete(`${API_URL}/api/v1/notification-rules/${n.id}`, { headers: h });
  for (const m of seed.monitors) await request.delete(`${API_URL}/api/v1/monitors/${m.id}`, { headers: h });
  for (const c of seed.chains) await request.delete(`${API_URL}/api/v1/chains/${c.id}`, { headers: h });
  for (const j of seed.jobs) await request.delete(`${API_URL}/api/v1/jobs/${j.id}`, { headers: h });
  for (const k of seed.pgpKeys) await request.delete(`${API_URL}/api/v1/pgp-keys/${k.id}`, { headers: h });
  for (const k of seed.sshKeys) await request.delete(`${API_URL}/api/v1/ssh-keys/${k.id}`, { headers: h });
  for (const c of seed.connections) await request.delete(`${API_URL}/api/v1/connections/${c.id}`, { headers: h });
  for (const t of seed.tags) await request.delete(`${API_URL}/api/v1/tags/${t.id}`, { headers: h });
}

// ── Screenshot Tests ──

test.describe.serial("Guide Screenshots", () => {
  let seed: SeedData;

  test.use({
    viewport: { width: 1440, height: 900 },
  });

  test.beforeAll(async ({ playwright }) => {
    const request = await playwright.request.newContext();
    seed = await seedData(request);
    await request.dispose();
  });

  test.afterAll(async ({ playwright }) => {
    const request = await playwright.request.newContext();
    await cleanupData(request, seed);
    await request.dispose();
  });

  // ── Login Page ──
  test("login page", async ({ page }) => {
    await page.goto("/login");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "login-page");
  });

  // ── Dashboard ──
  test("dashboard", async ({ page }) => {
    await authenticatePage(page);
    await page.waitForTimeout(2000); // let dashboard stats load
    await screenshot(page, "dashboard");
  });

  // ── Jobs ──
  test("jobs list", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/jobs");
    await page.getByRole("heading", { name: /jobs/i }).waitFor({ timeout: 10_000 });
    await page.waitForTimeout(1000);
    await screenshot(page, "jobs-list");
  });

  test("job create form", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/jobs/new");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "job-create");
  });

  test("job detail with steps", async ({ page }) => {
    await authenticatePage(page);
    if (!seed.jobs[0]) return;
    await page.goto(`/jobs/${seed.jobs[0].id}`);
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(2000);
    await screenshot(page, "job-detail");
  });

  test("job detail - steps tab", async ({ page }) => {
    await authenticatePage(page);
    if (!seed.jobs[0]) return;
    await page.goto(`/jobs/${seed.jobs[0].id}`);
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);

    // Click on Steps tab if it exists
    const stepsTab = page.getByRole("tab", { name: /steps/i });
    if (await stepsTab.isVisible()) {
      await stepsTab.click();
      await page.waitForTimeout(1000);
    }
    await screenshot(page, "job-steps");
  });

  test("job detail - executions tab", async ({ page }) => {
    await authenticatePage(page);
    if (!seed.jobs[0]) return;
    await page.goto(`/jobs/${seed.jobs[0].id}`);
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);

    const execTab = page.getByRole("tab", { name: /executions|history/i });
    if (await execTab.isVisible()) {
      await execTab.click();
      await page.waitForTimeout(1000);
    }
    await screenshot(page, "job-executions");
  });

  test("job detail - schedules tab", async ({ page }) => {
    await authenticatePage(page);
    if (!seed.jobs[0]) return;
    await page.goto(`/jobs/${seed.jobs[0].id}`);
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);

    const schedTab = page.getByRole("tab", { name: /schedule/i });
    if (await schedTab.isVisible()) {
      await schedTab.click();
      await page.waitForTimeout(1000);
    }
    await screenshot(page, "job-schedules");
  });

  // ── Connections ──
  test("connections list", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/connections");
    await page.getByRole("heading", { name: /connections/i }).waitFor({ timeout: 10_000 });
    await page.waitForTimeout(1000);
    await screenshot(page, "connections-list");
  });

  test("connection create form", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/connections/new");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "connection-create");
  });

  test("connection detail", async ({ page }) => {
    await authenticatePage(page);
    if (!seed.connections[0]) return;
    await page.goto(`/connections/${seed.connections[0].id}`);
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "connection-detail");
  });

  // ── Keys ──
  test("keys - PGP tab", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/keys");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "keys-pgp");
  });

  test("keys - SSH tab", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/keys/ssh");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "keys-ssh");
  });

  test("pgp key generate form", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/keys/pgp/new");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "pgp-key-generate");
  });

  test("ssh key generate form", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/keys/ssh/new");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "ssh-key-generate");
  });

  test("pgp key detail", async ({ page }) => {
    await authenticatePage(page);
    if (!seed.pgpKeys[0]) return;
    await page.goto(`/keys/pgp/${seed.pgpKeys[0].id}`);
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "pgp-key-detail");
  });

  // ── Chains ──
  test("chains list", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/chains");
    await page.getByRole("heading", { name: /chains/i }).waitFor({ timeout: 10_000 });
    await page.waitForTimeout(1000);
    await screenshot(page, "chains-list");
  });

  test("chain create form", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/chains/new");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "chain-create");
  });

  test("chain detail", async ({ page }) => {
    await authenticatePage(page);
    if (!seed.chains[0]) return;
    await page.goto(`/chains/${seed.chains[0].id}`);
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(2000);
    await screenshot(page, "chain-detail");
  });

  // ── Monitors ──
  test("monitors list", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/monitors");
    await page.getByRole("heading", { name: /monitors/i }).waitFor({ timeout: 10_000 });
    await page.waitForTimeout(1000);
    await screenshot(page, "monitors-list");
  });

  test("monitor create form", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/monitors/new");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "monitor-create");
  });

  test("monitor detail", async ({ page }) => {
    await authenticatePage(page);
    if (!seed.monitors[0]) return;
    await page.goto(`/monitors/${seed.monitors[0].id}`);
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "monitor-detail");
  });

  // ── Tags ──
  test("tags page", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/tags");
    await page.getByRole("heading", { name: /tags/i }).waitFor({ timeout: 10_000 });
    await page.waitForTimeout(1000);
    await screenshot(page, "tags-page");
  });

  // ── Notifications ──
  test("notifications list", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/notifications");
    await page.getByRole("heading", { name: /notification/i }).waitFor({ timeout: 10_000 });
    await page.waitForTimeout(1000);
    await screenshot(page, "notifications-list");
  });

  test("notification create form", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/notifications/new");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "notification-create");
  });

  // ── Audit ──
  test("audit log", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/audit");
    await page.getByRole("heading", { name: /audit/i }).waitFor({ timeout: 10_000 });
    await page.waitForTimeout(1000);
    await screenshot(page, "audit-log");
  });

  // ── Admin ──
  test("admin - users", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/admin/users");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "admin-users");
  });

  test("admin - settings", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/admin/settings");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "admin-settings");
  });

  // ── Account ──
  test("my account", async ({ page }) => {
    await authenticatePage(page);
    await page.goto("/account");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);
    await screenshot(page, "my-account");
  });

  // ── Sidebar collapsed ──
  test("sidebar collapsed", async ({ page }) => {
    await authenticatePage(page);
    // Click collapse button
    const collapseBtn = page.getByRole("button", { name: /collapse/i });
    if (await collapseBtn.isVisible()) {
      await collapseBtn.click();
      await page.waitForTimeout(500);
    }
    await screenshot(page, "sidebar-collapsed");
  });

  // ── User menu ──
  test("user menu dropdown", async ({ page }) => {
    await authenticatePage(page);
    // Click user menu in header
    const userBtn = page.locator("header").getByRole("button").last();
    await userBtn.click();
    await page.waitForTimeout(500);
    await screenshot(page, "user-menu");
  });
});
