import { test, expect } from "@playwright/test";
import { TEST_ADMIN } from "./global-setup";

// Login tests don't use the auth setup project — they test the login page itself.

test.describe("Login page", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/login");
  });

  test("displays login form", async ({ page }) => {
    await expect(page.getByLabel("Username")).toBeVisible();
    await expect(page.getByLabel("Password")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Sign In" })
    ).toBeVisible();
  });

  test("successful login redirects to dashboard", async ({ page }) => {
    await page.getByLabel("Username").fill(TEST_ADMIN.username);
    await page.getByLabel("Password").fill(TEST_ADMIN.password);
    await page.getByRole("button", { name: "Sign In" }).click();

    await expect(page).toHaveURL("/", { timeout: 10_000 });
  });

  test("shows error for invalid credentials", async ({ page }) => {
    await page.getByLabel("Username").fill(TEST_ADMIN.username);
    await page.getByLabel("Password").fill("WrongPassword123!");
    await page.getByRole("button", { name: "Sign In" }).click();

    await expect(page.locator(".bg-destructive\\/10")).toBeVisible();
  });

  test("shows error for non-existent user", async ({ page }) => {
    await page.getByLabel("Username").fill("nonexistentuser");
    await page.getByLabel("Password").fill("SomePassword123!");
    await page.getByRole("button", { name: "Sign In" }).click();

    await expect(page.locator(".bg-destructive\\/10")).toBeVisible();
  });

  test("password field masks input", async ({ page }) => {
    const passwordInput = page.getByLabel("Password");
    await expect(passwordInput).toHaveAttribute("type", "password");
  });

  test("login form validates required fields", async ({ page }) => {
    await page.getByRole("button", { name: "Sign In" }).click();

    // HTML5 required validation prevents submission — inputs should have required attribute
    await expect(page.getByLabel("Username")).toHaveAttribute("required", "");
    await expect(page.getByLabel("Password")).toHaveAttribute("required", "");

    // Should still be on login page (form not submitted)
    await expect(page).toHaveURL(/\/login/);
  });
});
