import { test, expect } from "./fixtures";
import {
  createTestUser,
  deleteTestUser,
  findUserByUsername,
  updateUser,
} from "./helpers/api-helpers";

test.describe("User Management", () => {
  test("displays users list with current admin", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/admin/users");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Users" }).first()
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      authenticatedPage.getByText("Manage user accounts and roles.")
    ).toBeVisible({ timeout: 10_000 });

    // The table should be visible with the testadmin user
    const table = authenticatedPage.locator("table");
    await expect(table).toBeVisible({ timeout: 10_000 });

    // Verify table headers (scoped to columnheader role to avoid matching data cells)
    await expect(table.getByRole("columnheader", { name: "Username" })).toBeVisible();
    await expect(table.getByRole("columnheader", { name: "Display Name" })).toBeVisible();
    await expect(table.getByRole("columnheader", { name: "Role" })).toBeVisible();
    await expect(table.getByRole("columnheader", { name: "Status" })).toBeVisible();

    // The testadmin user should be listed
    await expect(table.getByText("testadmin")).toBeVisible();
  });

  test("navigates to create user page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/admin/users");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Users" }).first()
    ).toBeVisible({ timeout: 10_000 });

    // Click the "Add User" button
    await authenticatedPage
      .getByRole("link", { name: "Add User" })
      .click();

    await expect(authenticatedPage).toHaveURL(/\/admin\/users\/new/, { timeout: 10_000 });
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create User" })
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      authenticatedPage.getByText("Add a new user account.")
    ).toBeVisible();
  });

  test("creates a new viewer user", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const username = `e2e-viewer-${suffix}`;

    await authenticatedPage.goto("/admin/users/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create User" })
    ).toBeVisible();

    // Fill in user details
    await authenticatedPage.getByLabel("Username").fill(username);
    await authenticatedPage
      .getByLabel("Display Name")
      .fill("E2E Test Viewer");
    await authenticatedPage
      .getByLabel("Email")
      .fill("e2e-viewer@test.local");

    // Role select — native <select> element, default is "viewer"
    // Verify the role select is present and set to viewer
    const roleSelect = authenticatedPage.locator("select#role");
    await expect(roleSelect).toHaveValue("viewer");

    // Fill passwords
    await authenticatedPage
      .getByLabel("Password", { exact: true })
      .fill("E2eViewerPass123!");
    await authenticatedPage
      .getByLabel("Confirm Password")
      .fill("E2eViewerPass123!");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create User" })
      .click();

    // Should redirect to users list with success toast
    await expect(authenticatedPage).toHaveURL("/admin/users", {
      timeout: 10_000,
    });
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "User created successfully" })
    ).toBeVisible();

    // Cleanup: find the created user via API and delete
    await expect(authenticatedPage.getByText(username)).toBeVisible();
    const created = await findUserByUsername(apiHelper.request, username);
    if (created) {
      await deleteTestUser(apiHelper.request, created.id);
    }
  });

  test("created user appears in list with correct role", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const user = await createTestUser(apiHelper.request, {
      username: `e2e-listrole-${Date.now().toString(36)}`,
      displayName: "E2E Role Check",
      role: "operator",
    });

    try {
      await authenticatedPage.goto("/admin/users");

      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Users" }).first()
      ).toBeVisible();

      // Wait for the table to load
      const table = authenticatedPage.locator("table");
      await expect(table).toBeVisible({ timeout: 10_000 });

      // The created user should be in the table
      const userRow = table.locator("tr", { hasText: user.username });
      await expect(userRow).toBeVisible();

      // Verify the role badge shows "operator"
      await expect(userRow.getByText("operator")).toBeVisible();

      // Verify display name
      await expect(userRow.getByText("E2E Role Check")).toBeVisible();
    } finally {
      await deleteTestUser(apiHelper.request, user.id);
    }
  });

  test("deletes a user", async ({ authenticatedPage, apiHelper }) => {
    const user = await createTestUser(apiHelper.request, {
      username: `e2e-deluser-${Date.now().toString(36)}`,
      displayName: "E2E Delete Target",
      role: "viewer",
    });

    // No finally — the test itself deletes the user
    await authenticatedPage.goto("/admin/users");

    // Wait for the table and the user to appear
    const table = authenticatedPage.locator("table");
    await expect(table).toBeVisible({ timeout: 10_000 });
    await expect(table.getByText(user.username)).toBeVisible();

    // Find the delete button in the user's row.
    // The delete button is a ghost icon button with Trash2 icon.
    const userRow = table.locator("tr", { hasText: user.username });

    // Register dialog handler BEFORE clicking — the page uses native confirm()
    authenticatedPage.once("dialog", async (dialog) => {
      await dialog.accept();
    });

    await userRow.getByRole("button").click();

    // Should see success toast
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "deleted" })
    ).toBeVisible({ timeout: 10_000 });

    // User should no longer appear in the list
    await expect(table.getByText(user.username)).not.toBeVisible();
  });

  test("user detail page displays all fields", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const user = await createTestUser(apiHelper.request, {
      username: `e2e-detail-${suffix}`,
      displayName: "E2E Detail User",
      email: "e2e-detail@test.local",
      role: "operator",
    });

    try {
      await authenticatedPage.goto("/admin/users");

      const table = authenticatedPage.locator("table");
      await expect(table).toBeVisible({ timeout: 10_000 });

      // Click the username link to go to detail page
      await table
        .getByRole("link", { name: user.username })
        .click();

      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/admin/users/${user.id}`),
        { timeout: 10_000 }
      );

      // Verify heading shows username
      await expect(
        authenticatedPage.getByRole("heading", { name: user.username })
      ).toBeVisible();

      // Verify "Created" date text is present
      await expect(
        authenticatedPage.getByText(/Created \d/)
      ).toBeVisible();

      // Verify form fields
      await expect(
        authenticatedPage.getByLabel("Display Name")
      ).toHaveValue("E2E Detail User");
      await expect(
        authenticatedPage.getByLabel("Email")
      ).toHaveValue("e2e-detail@test.local");
      await expect(
        authenticatedPage.locator("select#role")
      ).toHaveValue("operator");

      // Verify Account Active checkbox
      await expect(
        authenticatedPage.getByLabel("Account Active")
      ).toBeChecked();
    } finally {
      await deleteTestUser(apiHelper.request, user.id);
    }
  });

  test("edits user display name and email", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const user = await createTestUser(apiHelper.request, {
      username: `e2e-edit-${suffix}`,
      displayName: "E2E Before Edit",
      email: "before-edit@test.local",
      role: "viewer",
    });

    try {
      await authenticatedPage.goto(`/admin/users/${user.id}`);

      // Wait for user data to load
      await expect(
        authenticatedPage.getByRole("heading", { name: user.username })
      ).toBeVisible({ timeout: 10_000 });

      // Edit display name
      const displayNameInput = authenticatedPage.getByLabel("Display Name");
      await displayNameInput.clear();
      await displayNameInput.fill("E2E After Edit");

      // Edit email
      const emailInput = authenticatedPage.getByLabel("Email");
      await emailInput.clear();
      await emailInput.fill("after-edit@test.local");

      // Save
      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      // Verify success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "User updated successfully" })
      ).toBeVisible({ timeout: 10_000 });

      // Reload and verify changes persisted
      await authenticatedPage.reload();
      await expect(
        authenticatedPage.getByLabel("Display Name")
      ).toHaveValue("E2E After Edit", { timeout: 10_000 });
      await expect(
        authenticatedPage.getByLabel("Email")
      ).toHaveValue("after-edit@test.local");
    } finally {
      await deleteTestUser(apiHelper.request, user.id);
    }
  });

  test("resets user password", async ({ authenticatedPage, apiHelper }) => {
    const suffix = Date.now().toString(36);
    const user = await createTestUser(apiHelper.request, {
      username: `e2e-resetpw-${suffix}`,
      displayName: "E2E Reset PW",
      role: "viewer",
    });

    try {
      await authenticatedPage.goto(`/admin/users/${user.id}`);

      // Wait for user data to load
      await expect(
        authenticatedPage.getByRole("heading", { name: user.username })
      ).toBeVisible({ timeout: 10_000 });

      // Scroll to Reset Password section
      await expect(
        authenticatedPage.getByRole("heading", { name: "Reset Password" })
      ).toBeVisible();

      // Fill new password fields
      await authenticatedPage
        .getByLabel("New Password", { exact: true })
        .fill("NewResetPass123!");
      await authenticatedPage
        .getByLabel("Confirm New Password")
        .fill("NewResetPass123!");

      // Submit
      await authenticatedPage
        .getByRole("button", { name: "Reset Password" })
        .click();

      // Verify success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Password reset successfully" })
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestUser(apiHelper.request, user.id);
    }
  });

  test("creates user with admin role", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const username = `e2e-adminrole-${suffix}`;

    await authenticatedPage.goto("/admin/users/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create User" })
    ).toBeVisible();

    // Fill user details
    await authenticatedPage.getByLabel("Username").fill(username);
    await authenticatedPage
      .getByLabel("Display Name")
      .fill("E2E Admin Role");

    // Select admin role
    const roleSelect = authenticatedPage.locator("select#role");
    await roleSelect.selectOption("admin");
    await expect(roleSelect).toHaveValue("admin");

    // Fill passwords
    await authenticatedPage
      .getByLabel("Password", { exact: true })
      .fill("E2eAdminPass123!");
    await authenticatedPage
      .getByLabel("Confirm Password")
      .fill("E2eAdminPass123!");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create User" })
      .click();

    // Should redirect with success toast
    await expect(authenticatedPage).toHaveURL("/admin/users", {
      timeout: 10_000,
    });
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "User created successfully" })
    ).toBeVisible();

    // Verify user appears with admin role badge
    const table = authenticatedPage.locator("table");
    const userRow = table.locator("tr", { hasText: username });
    await expect(userRow).toBeVisible();
    await expect(
      userRow.locator("span").filter({ hasText: /^admin$/ })
    ).toBeVisible();

    // Cleanup
    const created = await findUserByUsername(apiHelper.request, username);
    if (created) {
      await deleteTestUser(apiHelper.request, created.id);
    }
  });

  test("searches users by username", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const user1 = await createTestUser(apiHelper.request, {
      username: `e2e-searchA-${suffix}`,
      displayName: "E2E Search A",
    });
    const user2 = await createTestUser(apiHelper.request, {
      username: `e2e-searchB-${suffix}`,
      displayName: "E2E Search B",
    });

    try {
      await authenticatedPage.goto("/admin/users");

      const table = authenticatedPage.locator("table");
      await expect(table).toBeVisible({ timeout: 10_000 });

      // Search for user1 specifically
      const searchInput = authenticatedPage.getByPlaceholder("Search users...");
      await searchInput.fill(user1.username);

      // Wait for the filtered results — user1 should be visible
      await expect(table.getByText(user1.username)).toBeVisible({
        timeout: 10_000,
      });

      // user2 should not be visible
      await expect(table.getByText(user2.username)).not.toBeVisible();
    } finally {
      await deleteTestUser(apiHelper.request, user1.id);
      await deleteTestUser(apiHelper.request, user2.id);
    }
  });

  test("pagination appears with many users", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create 11 users to exceed the page size of 10
    const suffix = Date.now().toString(36);
    const userIds: string[] = [];

    for (let i = 0; i < 11; i++) {
      const user = await createTestUser(apiHelper.request, {
        username: `e2e-page-${suffix}-${String(i).padStart(2, "0")}`,
        displayName: `E2E Page User ${i}`,
      });
      userIds.push(user.id);
    }

    try {
      await authenticatedPage.goto("/admin/users");

      const table = authenticatedPage.locator("table");
      await expect(table).toBeVisible({ timeout: 10_000 });

      // Pagination should appear (page size is 10, we created 11 + testadmin = 12)
      const paginationText = authenticatedPage.getByText(/Page \d+ of \d+/);
      await expect(paginationText).toBeVisible({ timeout: 10_000 });

      // Next button should be enabled on page 1
      const nextButton = authenticatedPage.getByRole("button", {
        name: "Next",
        exact: true,
      });
      await expect(nextButton).toBeEnabled();

      // Click next
      await nextButton.click();
      await expect(
        authenticatedPage.getByText(/Page 2 of \d+/)
      ).toBeVisible({ timeout: 10_000 });

      // Previous button should be enabled on page 2
      await expect(
        authenticatedPage.getByRole("button", {
          name: "Previous",
          exact: true,
        })
      ).toBeEnabled();
    } finally {
      for (const id of userIds) {
        await deleteTestUser(apiHelper.request, id);
      }
    }
  });

  test("self-delete prevention — no delete button for current user", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/admin/users");

    const table = authenticatedPage.locator("table");
    await expect(table).toBeVisible({ timeout: 10_000 });

    // Search for testadmin to ensure it's visible (may be on another page otherwise)
    const searchInput = authenticatedPage.getByPlaceholder("Search users...");
    await searchInput.fill("testadmin");

    // Find the testadmin row
    const adminRow = table.locator("tr", { hasText: "testadmin" });
    await expect(adminRow).toBeVisible({ timeout: 10_000 });

    // The delete button should NOT be present for the current user
    // (UI conditionally renders the button: u.id !== currentUser?.id)
    await expect(adminRow.getByRole("button")).not.toBeVisible();
  });

  test("toggles user active/inactive", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const user = await createTestUser(apiHelper.request, {
      username: `e2e-toggle-${suffix}`,
      displayName: "E2E Toggle Active",
      role: "viewer",
    });

    try {
      await authenticatedPage.goto(`/admin/users/${user.id}`);

      // Wait for user data to load
      await expect(
        authenticatedPage.getByRole("heading", { name: user.username })
      ).toBeVisible({ timeout: 10_000 });

      // Verify the active checkbox is checked by default
      const activeCheckbox = authenticatedPage.getByLabel("Account Active");
      await expect(activeCheckbox).toBeChecked();

      // Uncheck it to deactivate
      await activeCheckbox.uncheck();
      await expect(activeCheckbox).not.toBeChecked();

      // Save
      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "User updated successfully" })
      ).toBeVisible({ timeout: 10_000 });

      // Go back to users list and verify the status shows "Disabled"
      await authenticatedPage.goto("/admin/users");
      const table = authenticatedPage.locator("table");
      await expect(table).toBeVisible({ timeout: 10_000 });

      // Search for the user to ensure it's visible (may be on another page)
      const searchInput = authenticatedPage.getByPlaceholder("Search users...");
      await searchInput.fill(user.username);

      const userRow = table.locator("tr", { hasText: user.username });
      await expect(userRow).toBeVisible({ timeout: 10_000 });
      await expect(userRow.getByText("Disabled")).toBeVisible();
    } finally {
      // Re-activate the user before deleting to avoid side-effects on other tests
      try {
        await updateUser(apiHelper.request, user.id, { isActive: true });
      } catch {
        // Best-effort re-activation
      }
      try {
        await deleteTestUser(apiHelper.request, user.id);
      } catch {
        // Best-effort cleanup — don't mask test failures
      }
    }
  });
});
