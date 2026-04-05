import { test, expect } from "./fixtures";

test.describe("Admin — Security Settings", () => {
  test("security tab shows current settings", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/admin");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Administration" }).first()
    ).toBeVisible({ timeout: 10_000 });

    // Click the Security tab
    await authenticatedPage
      .getByRole("tab", { name: "Security" })
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
    await authenticatedPage.goto("/admin");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Administration" }).first()
    ).toBeVisible({ timeout: 10_000 });

    // Navigate to Security tab
    await authenticatedPage
      .getByRole("tab", { name: "Security" })
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
        .filter({ hasText: "Security settings updated" })
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
        .filter({ hasText: "Security settings updated" })
    ).toBeVisible();
  });

  test("all auth fields — modify refresh token days and restore", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/admin");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Administration" }).first()
    ).toBeVisible({ timeout: 10_000 });

    // Navigate to Security tab
    await authenticatedPage
      .getByRole("tab", { name: "Security" })
      .click();

    // Wait for all fields to load
    const accessTokenInput = authenticatedPage.getByLabel(
      "Access Token Lifetime (minutes)"
    );
    await expect(accessTokenInput).toBeVisible({ timeout: 10_000 });

    // Verify all fields exist
    const refreshDaysInput = authenticatedPage.getByLabel(
      "Refresh Token Lifetime (days)"
    );
    const minLengthInput = authenticatedPage.getByLabel("Minimum Length");
    const maxAttemptsInput = authenticatedPage.getByLabel(
      "Max Failed Attempts"
    );
    const lockoutInput = authenticatedPage.getByLabel(
      "Lockout Duration (minutes)"
    );

    await expect(refreshDaysInput).toBeVisible();
    await expect(minLengthInput).toBeVisible();
    await expect(maxAttemptsInput).toBeVisible();
    await expect(lockoutInput).toBeVisible();

    // Read original value of refresh token days
    const originalRefreshDays = await refreshDaysInput.inputValue();

    // Change refresh token days
    await refreshDaysInput.clear();
    await refreshDaysInput.fill("14");

    // Save
    await authenticatedPage
      .getByRole("button", { name: "Save Changes" })
      .click();

    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Security settings updated" })
    ).toBeVisible({ timeout: 10_000 });

    // Restore original value
    await refreshDaysInput.clear();
    await refreshDaysInput.fill(originalRefreshDays);
    await authenticatedPage
      .getByRole("button", { name: "Save Changes" })
      .click();

    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Security settings updated" })
    ).toBeVisible({ timeout: 10_000 });
  });

  test("lockout settings — modify and restore", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/admin");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Administration" }).first()
    ).toBeVisible({ timeout: 10_000 });

    // Navigate to Security tab
    await authenticatedPage
      .getByRole("tab", { name: "Security" })
      .click();

    // Wait for fields to load
    const maxAttemptsInput = authenticatedPage.getByLabel(
      "Max Failed Attempts"
    );
    await expect(maxAttemptsInput).toBeVisible({ timeout: 10_000 });

    const lockoutInput = authenticatedPage.getByLabel(
      "Lockout Duration (minutes)"
    );

    // Read originals
    const originalMaxAttempts = await maxAttemptsInput.inputValue();
    const originalLockout = await lockoutInput.inputValue();

    // Modify both
    await maxAttemptsInput.clear();
    await maxAttemptsInput.fill("10");
    await lockoutInput.clear();
    await lockoutInput.fill("30");

    // Save
    await authenticatedPage
      .getByRole("button", { name: "Save Changes" })
      .click();

    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Security settings updated" })
    ).toBeVisible({ timeout: 10_000 });

    // Verify the values are persisted by reloading
    await authenticatedPage.reload();

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Administration" }).first()
    ).toBeVisible({ timeout: 10_000 });

    await authenticatedPage
      .getByRole("tab", { name: "Security" })
      .click();
    await expect(maxAttemptsInput).toBeVisible({ timeout: 10_000 });
    await expect(maxAttemptsInput).toHaveValue("10");
    await expect(lockoutInput).toHaveValue("30");

    // Restore originals
    await maxAttemptsInput.clear();
    await maxAttemptsInput.fill(originalMaxAttempts);
    await lockoutInput.clear();
    await lockoutInput.fill(originalLockout);
    await authenticatedPage
      .getByRole("button", { name: "Save Changes" })
      .click();

    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Security settings updated" })
    ).toBeVisible({ timeout: 10_000 });
  });
});

test.describe("Account — Change Password", () => {
  // Run password tests serially because the password change test modifies
  // the user password, which would break parallel tests.
  test.describe.configure({ mode: "serial" });

  test("change password form renders", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/account");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "My Account" }).first()
    ).toBeVisible({ timeout: 10_000 });

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

  test("password validation — mismatched and too short", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/account");

    await expect(
      authenticatedPage.getByLabel("Current Password")
    ).toBeVisible({ timeout: 10_000 });

    // Try mismatched passwords
    await authenticatedPage
      .getByLabel("Current Password")
      .fill("TestPassword123!");
    await authenticatedPage
      .getByLabel("New Password", { exact: true })
      .fill("MismatchPass1!");
    await authenticatedPage
      .getByLabel("Confirm New Password")
      .fill("DifferentPass2!");

    await authenticatedPage
      .getByRole("button", { name: "Change Password" })
      .click();

    // Should see mismatch error toast
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "do not match" })
    ).toBeVisible({ timeout: 10_000 });
  });

  test("change password submit with temporary user", async ({
    browser,
    apiHelper,
  }) => {
    // Use a SEPARATE temporary user so we never touch testadmin's password.
    // This prevents cascading auth failures in parallel tests.
    const tempUsername = `e2e-pwchange-${Date.now()}`;
    const originalPassword = "OrigPwTest123!";
    const newPassword = "ChangedPwTest456!";

    const tempUser = await apiHelper.createTestUser(apiHelper.request, {
      username: tempUsername,
      displayName: "PW Change Tester",
      email: "pw-change@test.local",
      password: originalPassword,
      role: "admin",
    });

    try {
      // Log in as the temp user in a fresh browser context
      const apiUrl = process.env.API_URL || "http://localhost:5000";
      const context = await browser.newContext();
      const page = await context.newPage();

      const loginResp = await page.request.post(
        `${apiUrl}/api/v1/auth/login`,
        { data: { username: tempUsername, password: originalPassword } }
      );
      const loginBody = await loginResp.json();
      const { accessToken, refreshToken } = loginBody.data;

      await page.goto("/");
      await page.evaluate(
        ({ refreshToken }) => {
          localStorage.setItem("courier_refresh_token", refreshToken);
        },
        { refreshToken }
      );
      await page.evaluate(
        ({ accessToken }) => {
          sessionStorage.setItem("__e2e_access_token", accessToken);
        },
        { accessToken }
      );
      await page.reload();
      await page.waitForURL("/", { timeout: 10_000 });

      // Navigate to account page
      await page.goto("/account");

      await expect(
        page.getByLabel("Current Password")
      ).toBeVisible({ timeout: 10_000 });

      // Fill the change password form
      await page.getByLabel("Current Password").fill(originalPassword);
      await page
        .getByLabel("New Password", { exact: true })
        .fill(newPassword);
      await page.getByLabel("Confirm New Password").fill(newPassword);

      await page.getByRole("button", { name: "Change Password" }).click();

      await expect(
        page
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Password changed successfully" })
      ).toBeVisible({ timeout: 10_000 });

      await context.close();
    } finally {
      await apiHelper.deleteTestUser(apiHelper.request, tempUser.id);
    }
  });
});
