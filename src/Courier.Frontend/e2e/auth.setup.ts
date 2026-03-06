import { test as setup, expect } from "@playwright/test";
import { TEST_ADMIN } from "./global-setup";

setup("authenticate as admin", async ({ page }) => {
  await page.goto("/login");

  await page.getByLabel("Username").fill(TEST_ADMIN.username);
  await page.getByLabel("Password").fill(TEST_ADMIN.password);
  await page.getByRole("button", { name: "Sign In" }).click();

  // Wait for redirect to dashboard after login
  await expect(page).toHaveURL("/", { timeout: 10_000 });

  // Save signed-in state
  await page.context().storageState({ path: "e2e/.auth/admin.json" });
});
