/**
 * Fix the 3 broken screenshots: keys-ssh, admin-users, admin-settings.
 *
 * Run with:
 *   API_URL=http://localhost:60606 FRONTEND_URL=http://localhost:55674 \
 *     npx playwright test e2e/fix-screenshots.spec.ts --config=playwright.screenshots.config.ts --workers=1
 */
import { test } from "@playwright/test";
import path from "path";

const SCREENSHOT_DIR = path.resolve(__dirname, "../public/guide/screenshots");
const API_URL = process.env.API_URL || "http://localhost:5000";
const CREDENTIALS = {
  username: process.env.GUIDE_USER || "admin",
  password: process.env.GUIDE_PASS || "!Cc20080754",
};

async function screenshot(page: any, name: string) {
  await page.screenshot({
    path: path.join(SCREENSHOT_DIR, `${name}.png`),
    fullPage: false,
  });
}

async function login(page: any) {
  await page.goto("/login", { timeout: 30_000 });
  // Wait for login form to be fully rendered
  await page.getByLabel("Username").waitFor({ state: "visible", timeout: 15_000 });
  await page.getByLabel("Username").fill(CREDENTIALS.username);
  await page.getByLabel("Password").fill(CREDENTIALS.password);
  await page.getByRole("button", { name: "Sign In" }).click();
  await page.waitForURL("/", { timeout: 15_000 });
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(2000);
}

test.describe.serial("Fix Screenshots", () => {
  test.use({ viewport: { width: 1440, height: 900 } });
  test.setTimeout(60_000);

  test("keys - SSH tab (navigate via tab click)", async ({ page }) => {
    await login(page);
    await page.goto("/keys");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);

    // Click the SSH tab
    const sshTab = page.getByRole("tab", { name: /ssh/i }).or(
      page.getByRole("link", { name: /ssh/i })
    );
    if (await sshTab.isVisible()) {
      await sshTab.click();
      await page.waitForTimeout(1000);
    }
    await screenshot(page, "keys-ssh");
  });

  test("admin page (user management)", async ({ page }) => {
    await login(page);
    await page.goto("/admin");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(2000);
    await screenshot(page, "admin-users");
  });

  test("admin page - security tab", async ({ page }) => {
    await login(page);
    await page.goto("/admin");
    await page.waitForLoadState("networkidle");
    await page.waitForTimeout(1000);

    // Click the Security tab
    const securityTab = page.getByRole("tab", { name: /security/i });
    if (await securityTab.isVisible({ timeout: 5000 }).catch(() => false)) {
      await securityTab.click();
      await page.waitForTimeout(1000);
    }
    await screenshot(page, "admin-settings");
  });
});
