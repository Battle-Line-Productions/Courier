import { test, expect } from "./fixtures";
import {
  createTestUser,
  deleteTestUser,
  findUserByUsername,
} from "./helpers/api-helpers";

test.describe("User Management", () => {
  test("displays users list with current admin", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/settings/users");

    await expect(
      authenticatedPage.getByRole("heading", { name: "Users" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText("Manage user accounts and roles.")
    ).toBeVisible();

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
    await authenticatedPage.goto("/settings/users");

    await expect(
      authenticatedPage.getByRole("heading", { name: "Users" })
    ).toBeVisible();

    // Click the "Add User" button
    await authenticatedPage
      .getByRole("link", { name: "Add User" })
      .click();

    await expect(authenticatedPage).toHaveURL(/\/settings\/users\/new/);
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create User" })
    ).toBeVisible();
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

    await authenticatedPage.goto("/settings/users/new");
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
    await expect(authenticatedPage).toHaveURL("/settings/users", {
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
      await authenticatedPage.goto("/settings/users");

      await expect(
        authenticatedPage.getByRole("heading", { name: "Users" })
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
    await authenticatedPage.goto("/settings/users");

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
});
