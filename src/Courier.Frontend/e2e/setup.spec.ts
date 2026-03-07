import { test, expect } from "@playwright/test";

// These tests require a fresh database (no prior setup).
// Run separately: npx playwright test --project=fresh-db
// They are skipped automatically when the system is already initialized.

test.describe("Setup wizard @fresh-db", () => {
  // Skip all tests in this suite if the system is already initialized
  test.beforeEach(async ({ page }) => {
    const apiUrl = process.env.API_URL || "http://localhost:5000";
    const response = await page.request.post(`${apiUrl}/api/v1/auth/login`, {
      data: { username: "__probe__", password: "__probe__" },
    });
    // If login endpoint returns 401, the system is initialized (rejects bad creds)
    // If not initialized, it would return 503 or a different status
    if (response.status() === 401) {
      test.skip(true, "System already initialized — setup tests require a fresh database");
    }
  });

  test("redirects to setup when not initialized", async ({ page }) => {
    await page.goto("/");

    // Auth guard checks setup status and redirects to /setup
    await expect(page).toHaveURL(/\/setup/, { timeout: 10_000 });
  });

  test("displays setup form", async ({ page }) => {
    await page.goto("/setup");

    await expect(page.getByLabel("Username")).toBeVisible();
    await expect(page.getByLabel("Display Name")).toBeVisible();
    await expect(page.getByLabel("Email")).toBeVisible();
    await expect(page.getByLabel("Password", { exact: true })).toBeVisible();
    await expect(page.getByLabel("Confirm Password")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Create Admin Account" })
    ).toBeVisible();
  });

  test("validates password match", async ({ page }) => {
    await page.goto("/setup");

    await page.getByLabel("Username").fill("admin");
    await page.getByLabel("Display Name").fill("Admin User");
    await page.getByLabel("Password", { exact: true }).fill("TestPassword123!");
    await page.getByLabel("Confirm Password").fill("DifferentPassword!");
    await page.getByRole("button", { name: "Create Admin Account" }).click();

    await expect(page.locator(".bg-destructive\\/10")).toContainText(
      "Passwords do not match"
    );
  });

  test("successful setup creates admin and redirects", async ({ page }) => {
    await page.goto("/setup");

    await page.getByLabel("Username").fill("testadmin");
    await page.getByLabel("Display Name").fill("Test Admin");
    await page
      .getByLabel("Password", { exact: true })
      .fill("TestPassword123!");
    await page.getByLabel("Confirm Password").fill("TestPassword123!");
    await page.getByRole("button", { name: "Create Admin Account" }).click();

    // After setup + auto-login, should redirect to dashboard
    await expect(page).toHaveURL("/", { timeout: 15_000 });
  });
});
