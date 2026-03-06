import { test, expect } from "./fixtures";

test.describe("Settings", () => {
  test("displays settings page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/settings");

    await expect(
      authenticatedPage.getByRole("heading", { name: "Settings" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText("Manage application configuration.")
    ).toBeVisible();
  });

  test("authentication tab shows current settings", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/settings");

    await expect(
      authenticatedPage.getByRole("heading", { name: "Settings" })
    ).toBeVisible();

    // The Authentication tab should be active by default for admin users
    await authenticatedPage
      .getByRole("tab", { name: "Authentication" })
      .click();

    // Wait for auth settings to finish loading before checking sections
    await expect(
      authenticatedPage.getByLabel("Access Token Lifetime (minutes)")
    ).toBeVisible({ timeout: 10_000 });

    // Session section
    await expect(
      authenticatedPage.locator("h3", { hasText: "Session" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByLabel("Refresh Token Lifetime (days)")
    ).toBeVisible();

    // Password Policy section
    await expect(authenticatedPage.getByText("Password Policy")).toBeVisible();
    await expect(
      authenticatedPage.getByLabel("Minimum Length")
    ).toBeVisible();

    // Account Lockout section
    await expect(
      authenticatedPage.getByText("Account Lockout")
    ).toBeVisible();
    await expect(
      authenticatedPage.getByLabel("Max Failed Attempts")
    ).toBeVisible();
    await expect(
      authenticatedPage.getByLabel("Lockout Duration (minutes)")
    ).toBeVisible();

    // Save button
    await expect(
      authenticatedPage.getByRole("button", { name: "Save Changes" })
    ).toBeVisible();
  });

  test("updates session timeout setting", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/settings");

    // Navigate to Authentication tab
    await authenticatedPage
      .getByRole("tab", { name: "Authentication" })
      .click();

    // Wait for settings to load
    const sessionInput = authenticatedPage.getByLabel(
      "Access Token Lifetime (minutes)"
    );
    await expect(sessionInput).toBeVisible({ timeout: 10_000 });

    // Read the current value so we can restore it
    const originalValue = await sessionInput.inputValue();

    // Change the value
    await sessionInput.clear();
    await sessionInput.fill("45");

    // Save
    await authenticatedPage
      .getByRole("button", { name: "Save Changes" })
      .click();

    // Should see success toast
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Authentication settings updated" })
    ).toBeVisible();

    // Restore original value
    await sessionInput.clear();
    await sessionInput.fill(originalValue);
    await authenticatedPage
      .getByRole("button", { name: "Save Changes" })
      .click();

    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Authentication settings updated" })
    ).toBeVisible();
  });

  test("change password form renders", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/settings");

    // Click the Change Password tab
    await authenticatedPage
      .getByRole("tab", { name: "Change Password" })
      .click();

    // Verify form fields
    await expect(
      authenticatedPage.getByLabel("Current Password")
    ).toBeVisible();
    await expect(
      authenticatedPage.getByLabel("New Password", { exact: true })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByLabel("Confirm New Password")
    ).toBeVisible();

    // Submit button
    await expect(
      authenticatedPage.getByRole("button", { name: "Change Password" })
    ).toBeVisible();
  });

  test("SSO section shows current status", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/settings");

    // Click the SSO Providers tab
    await authenticatedPage
      .getByRole("tab", { name: "SSO Providers" })
      .click();

    // The SSO tab shows a placeholder/coming-soon message
    // Use heading role to avoid strict mode violation (tab button also contains "SSO Providers")
    await expect(
      authenticatedPage.locator("h3", { hasText: "SSO Providers" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText("Coming in a future update")
    ).toBeVisible();
  });
});
