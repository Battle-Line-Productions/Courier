import { test, expect } from "./fixtures";
import {
  generateTestPgpKey,
  deleteTestPgpKey,
  retirePgpKey,
  generateTestSshKey,
  deleteTestSshKey,
  retireSshKey,
} from "./helpers/api-helpers";

// ────────────────────────────────────────────────────────────────────
// PGP Keys
// ────────────────────────────────────────────────────────────────────

test.describe("Keys - PGP", () => {
  test("displays empty state for PGP keys", async ({ authenticatedPage }) => {
    // Navigate to keys page — PGP tab is the default
    await authenticatedPage.goto("/keys");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Keys", level: 1 })
    ).toBeVisible();

    // The PGP tab should be selected by default
    const pgpTab = authenticatedPage.getByRole("tab", { name: "PGP Keys" });
    await expect(pgpTab).toHaveAttribute("data-state", "active");

    // If no PGP keys exist, empty state is shown
    // (this test assumes a clean DB or runs before key creation tests)
    const emptyTitle = authenticatedPage.getByText("No PGP keys yet");
    if (await emptyTitle.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await expect(
        authenticatedPage.getByText(
          "Generate or import your first PGP key for encryption and signing operations."
        )
      ).toBeVisible();
      await expect(
        authenticatedPage.getByRole("link", { name: "Generate PGP Key" })
      ).toBeVisible();
    }
  });

  test("navigates to generate PGP key page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/keys");

    await authenticatedPage
      .getByRole("link", { name: "Generate" })
      .first()
      .click();

    await expect(authenticatedPage).toHaveURL(/\/keys\/pgp\/new/);
    await expect(
      authenticatedPage.getByRole("heading", { name: "Generate PGP Key" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText(
        "Generate a new PGP key pair for encryption and signing"
      )
    ).toBeVisible();
  });

  test("generates a new PGP key with ECC", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const keyName = `e2e-pgp-gen-${Date.now()}`;
    let createdKeyId: string | undefined;

    try {
      await authenticatedPage.goto("/keys/pgp/new");

      // Fill in key name
      await authenticatedPage.getByLabel("Name *").fill(keyName);

      // Select ECC Curve25519 algorithm
      await authenticatedPage
        .locator("form")
        .getByRole("combobox")
        .first()
        .click();
      await authenticatedPage
        .getByRole("option", { name: "ECC Curve25519" })
        .click();

      // Fill optional purpose
      await authenticatedPage
        .getByLabel("Purpose")
        .fill("E2E test encryption");

      // Submit
      await authenticatedPage
        .getByRole("button", { name: "Generate Key" })
        .click();

      // Should show success toast and redirect to detail page
      await expect(
        authenticatedPage.locator("[data-sonner-toast]").getByText("PGP key generated")
      ).toBeVisible({ timeout: 30_000 });

      await expect(authenticatedPage).toHaveURL(/\/keys\/pgp\/[0-9a-f-]+$/);

      // Capture the key id from URL for cleanup
      const url = authenticatedPage.url();
      createdKeyId = url.split("/keys/pgp/")[1];

      // Verify detail page shows the key name
      await expect(
        authenticatedPage.getByRole("heading", { name: keyName })
      ).toBeVisible();
    } finally {
      if (createdKeyId) {
        await deleteTestPgpKey(apiHelper.request, createdKeyId);
      }
    }
  });

  test("generated PGP key appears in list", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const keyName = `e2e-pgp-list-${Date.now()}`;
    const key = await generateTestPgpKey(apiHelper.request, keyName);

    try {
      await authenticatedPage.goto("/keys");

      // PGP tab is default
      const pgpTab = authenticatedPage.getByRole("tab", { name: "PGP Keys" });
      await expect(pgpTab).toHaveAttribute("data-state", "active");

      // Find the key name link in the table
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestPgpKey(apiHelper.request, key.id);
    }
  });

  test("PGP key detail page shows key info", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const keyName = `e2e-pgp-detail-${Date.now()}`;
    const key = await generateTestPgpKey(apiHelper.request, keyName);

    try {
      await authenticatedPage.goto(`/keys/pgp/${key.id}`);

      // Heading shows key name
      await expect(
        authenticatedPage.getByRole("heading", { name: keyName })
      ).toBeVisible();

      // Key Info card is present
      await expect(
        authenticatedPage.getByText("Key Info", { exact: true })
      ).toBeVisible();

      // Fingerprint label exists
      await expect(
        authenticatedPage.getByText("Fingerprint", { exact: true })
      ).toBeVisible();

      // Algorithm label and value
      await expect(
        authenticatedPage.getByText("Algorithm", { exact: true })
      ).toBeVisible();

      // Has Public Key / Has Private Key
      await expect(
        authenticatedPage.getByText("Has Public Key", { exact: true })
      ).toBeVisible();
      await expect(
        authenticatedPage.getByText("Has Private Key", { exact: true })
      ).toBeVisible();

      // Edit button
      await expect(
        authenticatedPage.getByRole("link", { name: "Edit" })
      ).toBeVisible();

      // Status badge shows "active"
      await expect(
        authenticatedPage.getByText("active", { exact: true }).first()
      ).toBeVisible();
    } finally {
      await deleteTestPgpKey(apiHelper.request, key.id);
    }
  });

  test("edits PGP key name and purpose", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const keyName = `e2e-pgp-edit-${Date.now()}`;
    const updatedName = `${keyName}-updated`;
    const key = await generateTestPgpKey(apiHelper.request, keyName);

    try {
      await authenticatedPage.goto(`/keys/pgp/${key.id}/edit`);

      // Verify edit page heading
      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit PGP Key" })
      ).toBeVisible();

      // Clear and update name
      const nameInput = authenticatedPage.getByLabel("Name");
      await nameInput.clear();
      await nameInput.fill(updatedName);

      // Update purpose
      const purposeInput = authenticatedPage.getByLabel("Purpose");
      await purposeInput.clear();
      await purposeInput.fill("Updated purpose");

      // Submit
      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      // Should show success toast
      await expect(
        authenticatedPage.locator("[data-sonner-toast]").getByText("PGP key updated")
      ).toBeVisible({ timeout: 10_000 });

      // Should redirect back to detail page with updated name
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/keys/pgp/${key.id}$`)
      );
      await expect(
        authenticatedPage.getByRole("heading", { name: updatedName })
      ).toBeVisible();

      // Purpose should be visible in Key Info section
      await expect(authenticatedPage.getByText("Updated purpose")).toBeVisible();
    } finally {
      await deleteTestPgpKey(apiHelper.request, key.id);
    }
  });

  test("retires a PGP key", async ({ authenticatedPage, apiHelper }) => {
    const keyName = `e2e-pgp-retire-${Date.now()}`;
    const key = await generateTestPgpKey(apiHelper.request, keyName);

    try {
      await authenticatedPage.goto("/keys");

      // Wait for the key to appear in the list
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).toBeVisible({ timeout: 10_000 });

      // Find the key row and open its dropdown menu
      const row = authenticatedPage.getByRole("row").filter({ hasText: keyName });
      // Force visibility of the action button (it's opacity-0 until hover)
      const actionButton = row.getByRole("button");
      await actionButton.click({ force: true });

      // Click Retire from dropdown
      await authenticatedPage
        .getByRole("menuitem", { name: "Retire" })
        .click();

      // Should show success toast
      await expect(
        authenticatedPage.locator("[data-sonner-toast]").getByText("Key retired")
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestPgpKey(apiHelper.request, key.id);
    }
  });

  test("activates a retired PGP key", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const keyName = `e2e-pgp-activate-${Date.now()}`;
    const key = await generateTestPgpKey(apiHelper.request, keyName);

    // Retire it via API first
    await retirePgpKey(apiHelper.request, key.id);

    try {
      await authenticatedPage.goto("/keys");

      // Switch status filter to show retired keys
      await authenticatedPage
        .locator(".w-36")
        .first()
        .click();
      await authenticatedPage
        .getByRole("option", { name: "Retired" })
        .click();

      // Type in search to narrow results
      await authenticatedPage
        .getByPlaceholder("Search PGP keys...")
        .fill(keyName);

      // Wait for filtered results
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).toBeVisible({ timeout: 10_000 });

      // Open the row action menu
      const row = authenticatedPage.getByRole("row").filter({ hasText: keyName });
      const actionButton = row.getByRole("button");
      await actionButton.click({ force: true });

      // Click Activate from dropdown
      await authenticatedPage
        .getByRole("menuitem", { name: "Activate" })
        .click();

      // Should show success toast
      await expect(
        authenticatedPage.locator("[data-sonner-toast]").getByText("Key activated")
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestPgpKey(apiHelper.request, key.id);
    }
  });

  test("filters PGP keys by status", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const keyName = `e2e-pgp-filter-${Date.now()}`;
    const key = await generateTestPgpKey(apiHelper.request, keyName);

    try {
      await authenticatedPage.goto("/keys");

      // Wait for key to appear
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).toBeVisible({ timeout: 10_000 });

      // Open the status filter dropdown
      const statusTrigger = authenticatedPage.locator("button", {
        hasText: "Status",
      });
      // Try the select trigger for status
      await authenticatedPage
        .locator(".w-36")
        .first()
        .click();

      // Select "Active"
      await authenticatedPage
        .getByRole("option", { name: "Active" })
        .click();

      // The key should still be visible (it's active)
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).toBeVisible();

      // Now filter by "Retired" — our active key should disappear
      await authenticatedPage
        .locator(".w-36")
        .first()
        .click();
      await authenticatedPage
        .getByRole("option", { name: "Retired" })
        .click();

      // Either no results message or the key is not visible
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).not.toBeVisible({ timeout: 5_000 });
    } finally {
      await deleteTestPgpKey(apiHelper.request, key.id);
    }
  });

  test("searches PGP keys by name", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const uniqueSuffix = Date.now().toString();
    const keyName = `e2e-pgp-search-${uniqueSuffix}`;
    const key = await generateTestPgpKey(apiHelper.request, keyName);

    try {
      await authenticatedPage.goto("/keys");

      // Wait for the key to appear
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).toBeVisible({ timeout: 10_000 });

      // Type a search that should match
      await authenticatedPage
        .getByPlaceholder("Search PGP keys...")
        .fill(uniqueSuffix);

      // The key should still be visible
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).toBeVisible();

      // Type a search that should NOT match
      await authenticatedPage
        .getByPlaceholder("Search PGP keys...")
        .fill("zzz-no-match-zzz");

      // The key should disappear
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).not.toBeVisible({ timeout: 5_000 });
    } finally {
      await deleteTestPgpKey(apiHelper.request, key.id);
    }
  });

  test("deletes a PGP key", async ({ authenticatedPage, apiHelper }) => {
    const keyName = `e2e-pgp-delete-${Date.now()}`;
    const key = await generateTestPgpKey(apiHelper.request, keyName);

    await authenticatedPage.goto("/keys");

    // Wait for the key to appear
    await expect(
      authenticatedPage.getByRole("link", { name: keyName })
    ).toBeVisible({ timeout: 10_000 });

    // Open the row action menu
    const row = authenticatedPage.getByRole("row").filter({ hasText: keyName });
    const actionButton = row.getByRole("button");
    await actionButton.click({ force: true });

    // Click Delete from dropdown
    await authenticatedPage
      .getByRole("menuitem", { name: "Delete" })
      .click();

    // Confirm dialog should appear
    const dialog = authenticatedPage.getByRole("dialog");
    await expect(dialog.getByText("Delete PGP Key")).toBeVisible();
    await expect(
      dialog.getByText(`Are you sure you want to delete "${keyName}"?`)
    ).toBeVisible();

    // Click the confirm Delete button in the dialog
    await dialog.getByRole("button", { name: "Delete" }).click();

    // Should show success toast
    await expect(
      authenticatedPage.locator("[data-sonner-toast]").getByText("PGP key deleted")
    ).toBeVisible({ timeout: 10_000 });

    // Key should no longer appear in the list
    await expect(
      authenticatedPage.getByRole("link", { name: keyName })
    ).not.toBeVisible({ timeout: 5_000 });
  });
});

// ────────────────────────────────────────────────────────────────────
// SSH Keys
// ────────────────────────────────────────────────────────────────────

test.describe("Keys - SSH", () => {
  test("switches to SSH tab", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/keys");

    // Click the SSH tab
    const sshTab = authenticatedPage.getByRole("tab", { name: "SSH Keys" });
    await sshTab.click();

    await expect(sshTab).toHaveAttribute("data-state", "active");
  });

  test("displays empty state for SSH keys", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/keys");

    // Click the SSH tab
    await authenticatedPage
      .getByRole("tab", { name: "SSH Keys" })
      .click();

    // If no SSH keys exist, empty state is shown
    const emptyTitle = authenticatedPage.getByText("No SSH keys yet");
    if (await emptyTitle.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await expect(
        authenticatedPage.getByText(
          "Generate or import your first SSH key for SFTP authentication."
        )
      ).toBeVisible();
      await expect(
        authenticatedPage.getByRole("link", { name: "Generate SSH Key" })
      ).toBeVisible();
    }
  });

  test("navigates to generate SSH key page", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/keys");

    // Switch to SSH tab so Generate button links to SSH
    await authenticatedPage
      .getByRole("tab", { name: "SSH Keys" })
      .click();

    // Use the empty state link or scope to the tab panel to avoid matching
    // the PGP "Generate" link in the header as well
    const sshTabPanel = authenticatedPage.getByRole("tabpanel", { name: "SSH Keys" });
    const emptyStateLink = sshTabPanel.getByRole("link", { name: "Generate SSH Key" });
    const headerLink = authenticatedPage.getByRole("link", { name: "Generate" });

    // Prefer the empty-state link if visible, otherwise use the first header link
    if (await emptyStateLink.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await emptyStateLink.click();
    } else {
      await headerLink.first().click();
    }

    await expect(authenticatedPage).toHaveURL(/\/keys\/ssh\/new/);
    await expect(
      authenticatedPage.getByRole("heading", { name: "Generate SSH Key" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText(
        "Generate a new SSH key pair for SFTP authentication"
      )
    ).toBeVisible();
  });

  test("generates a new SSH key with Ed25519", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const keyName = `e2e-ssh-gen-${Date.now()}`;
    let createdKeyId: string | undefined;

    try {
      await authenticatedPage.goto("/keys/ssh/new");

      // Fill in key name
      await authenticatedPage.getByLabel("Name *").fill(keyName);

      // Ed25519 is the default key type — verify it's selected
      await expect(
        authenticatedPage.locator("form").getByRole("combobox").first()
      ).toHaveText(/Ed25519/);

      // Submit
      await authenticatedPage
        .getByRole("button", { name: "Generate Key" })
        .click();

      // Should show success toast and redirect to detail page
      await expect(
        authenticatedPage.locator("[data-sonner-toast]").getByText("SSH key generated")
      ).toBeVisible({ timeout: 30_000 });

      await expect(authenticatedPage).toHaveURL(/\/keys\/ssh\/[0-9a-f-]+$/);

      // Capture the key id from URL for cleanup
      const url = authenticatedPage.url();
      createdKeyId = url.split("/keys/ssh/")[1];

      // Verify detail page shows the key name
      await expect(
        authenticatedPage.getByRole("heading", { name: keyName })
      ).toBeVisible();
    } finally {
      if (createdKeyId) {
        await deleteTestSshKey(apiHelper.request, createdKeyId);
      }
    }
  });

  test("SSH key detail page shows fingerprint", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const keyName = `e2e-ssh-detail-${Date.now()}`;
    const key = await generateTestSshKey(apiHelper.request, keyName);

    try {
      await authenticatedPage.goto(`/keys/ssh/${key.id}`);

      // Heading shows key name
      await expect(
        authenticatedPage.getByRole("heading", { name: keyName })
      ).toBeVisible();

      // Key Info card
      await expect(
        authenticatedPage.getByText("Key Info", { exact: true })
      ).toBeVisible();

      // Fingerprint label
      await expect(
        authenticatedPage.getByText("Fingerprint", { exact: true })
      ).toBeVisible();

      // Key Type label
      await expect(
        authenticatedPage.getByText("Key Type", { exact: true })
      ).toBeVisible();

      // Has Public Key / Has Private Key
      await expect(
        authenticatedPage.getByText("Has Public Key", { exact: true })
      ).toBeVisible();
      await expect(
        authenticatedPage.getByText("Has Private Key", { exact: true })
      ).toBeVisible();

      // Status shows active
      await expect(
        authenticatedPage.getByText("active", { exact: true }).first()
      ).toBeVisible();

      // Edit button
      await expect(
        authenticatedPage.getByRole("link", { name: "Edit" })
      ).toBeVisible();
    } finally {
      await deleteTestSshKey(apiHelper.request, key.id);
    }
  });

  test("edits SSH key name", async ({ authenticatedPage, apiHelper }) => {
    const keyName = `e2e-ssh-edit-${Date.now()}`;
    const updatedName = `${keyName}-updated`;
    const key = await generateTestSshKey(apiHelper.request, keyName);

    try {
      await authenticatedPage.goto(`/keys/ssh/${key.id}/edit`);

      // Verify edit page heading
      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit SSH Key" })
      ).toBeVisible();

      // Clear and update name
      const nameInput = authenticatedPage.getByLabel("Name");
      await nameInput.clear();
      await nameInput.fill(updatedName);

      // Submit
      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      // Should show success toast
      await expect(
        authenticatedPage.locator("[data-sonner-toast]").getByText("SSH key updated")
      ).toBeVisible({ timeout: 10_000 });

      // Should redirect back to detail page with updated name
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/keys/ssh/${key.id}$`)
      );
      await expect(
        authenticatedPage.getByRole("heading", { name: updatedName })
      ).toBeVisible();
    } finally {
      await deleteTestSshKey(apiHelper.request, key.id);
    }
  });

  test("retires an SSH key", async ({ authenticatedPage, apiHelper }) => {
    const keyName = `e2e-ssh-retire-${Date.now()}`;
    const key = await generateTestSshKey(apiHelper.request, keyName);

    try {
      await authenticatedPage.goto("/keys");

      // Switch to SSH tab
      await authenticatedPage
        .getByRole("tab", { name: "SSH Keys" })
        .click();

      // Wait for key to appear
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).toBeVisible({ timeout: 10_000 });

      // Open the row action menu
      const row = authenticatedPage.getByRole("row").filter({ hasText: keyName });
      const actionButton = row.getByRole("button");
      await actionButton.click({ force: true });

      // Click Retire from dropdown
      await authenticatedPage
        .getByRole("menuitem", { name: "Retire" })
        .click();

      // Should show success toast
      await expect(
        authenticatedPage.locator("[data-sonner-toast]").getByText("Key retired")
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestSshKey(apiHelper.request, key.id);
    }
  });

  test("activates a retired SSH key", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const keyName = `e2e-ssh-activate-${Date.now()}`;
    const key = await generateTestSshKey(apiHelper.request, keyName);

    // Retire it via API first
    await retireSshKey(apiHelper.request, key.id);

    try {
      await authenticatedPage.goto("/keys");

      // Switch to SSH tab
      await authenticatedPage
        .getByRole("tab", { name: "SSH Keys" })
        .click();

      // Search for our key
      await authenticatedPage
        .getByPlaceholder("Search SSH keys...")
        .fill(keyName);

      // Wait for the key to appear
      await expect(
        authenticatedPage.getByRole("link", { name: keyName })
      ).toBeVisible({ timeout: 10_000 });

      // Open the row action menu
      const row = authenticatedPage.getByRole("row").filter({ hasText: keyName });
      const actionButton = row.getByRole("button");
      await actionButton.click({ force: true });

      // Click Activate from dropdown
      await authenticatedPage
        .getByRole("menuitem", { name: "Activate" })
        .click();

      // Should show success toast
      await expect(
        authenticatedPage.locator("[data-sonner-toast]").getByText("Key activated")
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestSshKey(apiHelper.request, key.id);
    }
  });

  test("deletes an SSH key", async ({ authenticatedPage, apiHelper }) => {
    const keyName = `e2e-ssh-delete-${Date.now()}`;
    const key = await generateTestSshKey(apiHelper.request, keyName);

    await authenticatedPage.goto("/keys");

    // Switch to SSH tab
    await authenticatedPage
      .getByRole("tab", { name: "SSH Keys" })
      .click();

    // Wait for the key to appear
    await expect(
      authenticatedPage.getByRole("link", { name: keyName })
    ).toBeVisible({ timeout: 10_000 });

    // Open the row action menu
    const row = authenticatedPage.getByRole("row").filter({ hasText: keyName });
    const actionButton = row.getByRole("button");
    await actionButton.click({ force: true });

    // Click Delete from dropdown
    await authenticatedPage
      .getByRole("menuitem", { name: "Delete" })
      .click();

    // Confirm dialog should appear
    const dialog = authenticatedPage.getByRole("dialog");
    await expect(dialog.getByText("Delete SSH Key")).toBeVisible();
    await expect(
      dialog.getByText(`Are you sure you want to delete "${keyName}"?`)
    ).toBeVisible();

    // Click the confirm Delete button in the dialog
    await dialog.getByRole("button", { name: "Delete" }).click();

    // Should show success toast
    await expect(
      authenticatedPage.locator("[data-sonner-toast]").getByText("SSH key deleted")
    ).toBeVisible({ timeout: 10_000 });

    // Key should no longer appear in the list
    await expect(
      authenticatedPage.getByRole("link", { name: keyName })
    ).not.toBeVisible({ timeout: 5_000 });
  });
});
