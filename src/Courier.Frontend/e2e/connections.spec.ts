import { test, expect } from "./fixtures";
import {
  createTestConnection,
  deleteTestConnection,
} from "./helpers/api-helpers";

test.describe("Connections", () => {
  test("displays empty state when no connections exist", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create and delete to ensure we can navigate, then check
    const conn = await createTestConnection(apiHelper.request, {
      name: "e2e-empty-check",
    });
    await deleteTestConnection(apiHelper.request, conn.id);

    await authenticatedPage.goto("/connections");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Connections", exact: true }).first()
    ).toBeVisible();

    // Either empty state or table depending on existing data
    const emptyState = authenticatedPage.getByText("No connections yet");
    const connTable = authenticatedPage.locator("table");
    await expect(emptyState.or(connTable)).toBeVisible();
  });

  test("navigates to create connection page", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/connections");
    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Connections", exact: true }).first()
    ).toBeVisible();

    await authenticatedPage
      .getByRole("link", { name: "Create Connection" })
      .first()
      .click();
    await expect(authenticatedPage).toHaveURL(/\/connections\/new/);
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();
  });

  test("creates an SFTP connection with password auth", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const connName = `e2e-sftp-create-${suffix}`;

    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();

    // Fill in Core Settings — Protocol defaults to SFTP, no need to change it
    // Use { exact: true } because "Username" also contains "Name"
    await authenticatedPage.getByLabel("Name", { exact: true }).fill(connName);
    await authenticatedPage.getByLabel("Host").fill("sftp.test.example.com");

    // Port defaults to 22 — verify
    await expect(authenticatedPage.getByLabel("Port")).toHaveValue("22");

    // Fill Authentication
    await authenticatedPage.getByLabel("Username").fill("testuser");
    await authenticatedPage.getByLabel("Password").fill("testpassword123");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create Connection" })
      .click();

    // Should redirect to the connection detail page with success toast
    await expect(authenticatedPage).toHaveURL(/\/connections\/[\w-]+$/, {
      timeout: 10_000,
    });
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Connection created" })
    ).toBeVisible({ timeout: 10_000 });

    // Verify we're on the detail page
    await expect(
      authenticatedPage.getByRole("heading", { name: connName })
    ).toBeVisible();

    // Cleanup via API
    const url = authenticatedPage.url();
    const id = url.split("/connections/")[1];
    if (id) {
      await deleteTestConnection(apiHelper.request, id);
    }
  });

  test("created connection appears in list", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const conn = await createTestConnection(apiHelper.request, {
      name: `e2e-list-${Date.now().toString(36)}`,
      host: "list.example.com",
    });

    try {
      await authenticatedPage.goto("/connections");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Connections", exact: true }).first()
      ).toBeVisible();

      // Connection name should appear as a link in the table
      await expect(
        authenticatedPage.getByRole("link", { name: conn.name })
      ).toBeVisible();

      // Host should be visible
      await expect(
        authenticatedPage.getByText(`${conn.host}:${conn.port}`)
      ).toBeVisible();
    } finally {
      await deleteTestConnection(apiHelper.request, conn.id);
    }
  });

  test("connection detail page shows connection info", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const conn = await createTestConnection(apiHelper.request, {
      name: `e2e-detail-${Date.now().toString(36)}`,
      host: "detail.example.com",
      port: 2222,
      username: "detailuser",
    });

    try {
      await authenticatedPage.goto(`/connections/${conn.id}`);

      // Heading is the connection name
      await expect(
        authenticatedPage.getByRole("heading", { name: conn.name })
      ).toBeVisible();

      // Protocol badge should be visible
      await expect(
        authenticatedPage.locator("main").getByText("SFTP", { exact: false }).first()
      ).toBeVisible();

      // Connection Info card
      await expect(
        authenticatedPage.getByText("Connection Info")
      ).toBeVisible();

      // Host:port shown
      await expect(
        authenticatedPage.getByText(`${conn.host}:${conn.port}`)
      ).toBeVisible();

      // Username shown
      await expect(
        authenticatedPage.getByText("detailuser")
      ).toBeVisible();

      // Test Connection button should be visible (but we do NOT click it)
      await expect(
        authenticatedPage.getByRole("button", { name: "Test Connection" })
      ).toBeVisible();

      // Edit button should be visible
      await expect(
        authenticatedPage.getByRole("link", { name: "Edit" })
      ).toBeVisible();

      // Settings card should be visible for file transfer protocols
      await expect(
        authenticatedPage.locator("main").getByText("Settings", { exact: true })
      ).toBeVisible();
    } finally {
      await deleteTestConnection(apiHelper.request, conn.id);
    }
  });

  test("edits connection name and description", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const conn = await createTestConnection(apiHelper.request, {
      name: `e2e-edit-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto(`/connections/${conn.id}/edit`);
      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit Connection" })
      ).toBeVisible();

      // Change the name — use exact to avoid matching "Username"
      const nameInput = authenticatedPage.getByLabel("Name", { exact: true });
      await nameInput.clear();
      const newName = `e2e-edited-${Date.now().toString(36)}`;
      await nameInput.fill(newName);

      // Add notes
      const notesInput = authenticatedPage.getByLabel("Notes");
      await notesInput.fill("Updated by E2E test");

      // Submit
      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      // Should redirect to connection detail page with success toast
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/connections/${conn.id}$`),
        { timeout: 10_000 }
      );
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Connection updated" })
      ).toBeVisible({ timeout: 10_000 });

      // Verify updated name on the detail page
      await expect(
        authenticatedPage.getByRole("heading", { name: newName })
      ).toBeVisible();
    } finally {
      await deleteTestConnection(apiHelper.request, conn.id);
    }
  });

  test("filters connections by protocol", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const sftpConn = await createTestConnection(apiHelper.request, {
      name: `e2e-proto-sftp-${suffix}`,
      protocol: "sftp",
      port: 22,
    });
    const ftpConn = await createTestConnection(apiHelper.request, {
      name: `e2e-proto-ftp-${suffix}`,
      protocol: "ftp",
      host: "ftp.test.example.com",
      port: 21,
    });

    try {
      await authenticatedPage.goto("/connections");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Connections", exact: true }).first()
      ).toBeVisible();

      // Wait for both connections to be visible
      await expect(
        authenticatedPage.getByText(sftpConn.name)
      ).toBeVisible();

      // Open the Protocol filter combobox and select SFTP
      const protocolTrigger = authenticatedPage
        .getByRole("combobox")
        .filter({ hasText: /Protocol|All Protocols|SFTP|FTP|FTPS/ })
        .first();
      await protocolTrigger.click();
      await authenticatedPage
        .getByRole("option", { name: "SFTP", exact: true })
        .click();

      // SFTP connection should be visible, FTP connection should not
      await expect(
        authenticatedPage.getByText(sftpConn.name)
      ).toBeVisible();
      await expect(
        authenticatedPage.getByText(ftpConn.name)
      ).not.toBeVisible({ timeout: 5_000 });
    } finally {
      await deleteTestConnection(apiHelper.request, sftpConn.id);
      await deleteTestConnection(apiHelper.request, ftpConn.id);
    }
  });

  test("filters connections by status", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const conn = await createTestConnection(apiHelper.request, {
      name: `e2e-status-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto("/connections");
      await expect(
        authenticatedPage.getByText(conn.name)
      ).toBeVisible();

      // Open the Status filter combobox and select Active
      const statusTrigger = authenticatedPage
        .getByRole("combobox")
        .filter({ hasText: /Status|All Statuses|Active|Disabled/ })
        .first();
      await statusTrigger.click();
      await authenticatedPage
        .getByRole("option", { name: "Active" })
        .click();

      // Connection was created as active, so it should still be visible
      await expect(
        authenticatedPage.getByText(conn.name)
      ).toBeVisible();

      // Now filter to Disabled — connection should disappear
      await statusTrigger.click();
      await authenticatedPage
        .getByRole("option", { name: "Disabled" })
        .click();

      await expect(
        authenticatedPage.getByText(conn.name)
      ).not.toBeVisible({ timeout: 5_000 });
    } finally {
      await deleteTestConnection(apiHelper.request, conn.id);
    }
  });

  test("searches connections by name", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const conn1 = await createTestConnection(apiHelper.request, {
      name: `e2e-findme-${suffix}`,
    });
    const conn2 = await createTestConnection(apiHelper.request, {
      name: `e2e-hideme-${suffix}`,
    });

    try {
      await authenticatedPage.goto("/connections");
      await expect(
        authenticatedPage.getByText(conn1.name)
      ).toBeVisible();

      const searchInput = authenticatedPage.getByPlaceholder(
        "Search connections..."
      );
      await searchInput.fill("e2e-findme");

      await expect(
        authenticatedPage.getByText(conn1.name)
      ).toBeVisible();
      await expect(
        authenticatedPage.getByText(conn2.name)
      ).not.toBeVisible();
    } finally {
      await deleteTestConnection(apiHelper.request, conn1.id);
      await deleteTestConnection(apiHelper.request, conn2.id);
    }
  });

  test("form changes port when protocol changes", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();

    const portInput = authenticatedPage.getByLabel("Port");

    // Default protocol is SFTP, port should be 22
    await expect(portInput).toHaveValue("22");

    // Change protocol to FTP — click the Protocol combobox trigger
    const protocolTrigger = authenticatedPage
      .getByRole("combobox")
      .filter({ hasText: "SFTP" });
    await protocolTrigger.click();
    await authenticatedPage
      .getByRole("option", { name: "FTP", exact: true })
      .click();

    // Port should auto-change to 21
    await expect(portInput).toHaveValue("21");

    // Change to FTPS
    await authenticatedPage
      .getByRole("combobox")
      .filter({ hasText: "FTP" })
      .first()
      .click();
    await authenticatedPage
      .getByRole("option", { name: "FTPS", exact: true })
      .click();

    // Port should auto-change to 990
    await expect(portInput).toHaveValue("990");
  });

  test("creates an FTP connection", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const connName = `e2e-ftp-${suffix}`;

    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();

    // Fill Name — use exact to avoid matching "Username"
    await authenticatedPage.getByLabel("Name", { exact: true }).fill(connName);

    // Change protocol to FTP — click the Protocol combobox trigger
    await authenticatedPage
      .getByRole("combobox")
      .filter({ hasText: "SFTP" })
      .click();
    await authenticatedPage
      .getByRole("option", { name: "FTP", exact: true })
      .click();

    // Fill host
    await authenticatedPage.getByLabel("Host").fill("ftp.test.example.com");

    // Port should already be 21
    await expect(authenticatedPage.getByLabel("Port")).toHaveValue("21");

    // Fill auth
    await authenticatedPage.getByLabel("Username").fill("ftpuser");
    await authenticatedPage.getByLabel("Password").fill("ftppass123");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create Connection" })
      .click();

    // Should redirect to detail page
    await expect(authenticatedPage).toHaveURL(/\/connections\/[\w-]+$/, {
      timeout: 10_000,
    });
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Connection created" })
    ).toBeVisible({ timeout: 10_000 });

    await expect(
      authenticatedPage.getByRole("heading", { name: connName })
    ).toBeVisible();

    // Cleanup
    const url = authenticatedPage.url();
    const id = url.split("/connections/")[1];
    if (id) {
      await deleteTestConnection(apiHelper.request, id);
    }
  });

  test("deletes a connection", async ({ authenticatedPage, apiHelper }) => {
    const conn = await createTestConnection(apiHelper.request, {
      name: `e2e-delete-${Date.now().toString(36)}`,
    });

    await authenticatedPage.goto("/connections");
    await expect(
      authenticatedPage.getByText(conn.name)
    ).toBeVisible();

    // Open the actions dropdown for the connection row.
    // Force-click because the trigger has opacity-0 until group-hover.
    const row = authenticatedPage.locator("tr", { hasText: conn.name });
    await row
      .getByRole("button")
      .filter({ has: authenticatedPage.locator("svg") })
      .click({ force: true });

    // Click Delete in the dropdown
    await authenticatedPage
      .getByRole("menuitem", { name: "Delete" })
      .click();

    // Confirm dialog appears
    const dialog = authenticatedPage.locator("[role=dialog]");
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText("Delete Connection")).toBeVisible();

    // Click the confirm Delete button
    await dialog.getByRole("button", { name: "Delete" }).click();

    // Success toast
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Connection deleted" })
    ).toBeVisible();

    // Connection should no longer appear in the list
    await expect(
      authenticatedPage.getByText(conn.name)
    ).not.toBeVisible();
  });

  test("creates an FTPS connection", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const connName = `e2e-ftps-${suffix}`;

    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();

    // Fill Name
    await authenticatedPage.getByLabel("Name", { exact: true }).fill(connName);

    // Change protocol to FTPS
    await authenticatedPage
      .getByRole("combobox")
      .filter({ hasText: "SFTP" })
      .click();
    await authenticatedPage
      .getByRole("option", { name: "FTPS", exact: true })
      .click();

    // Fill host
    await authenticatedPage.getByLabel("Host").fill("ftps.test.example.com");

    // Port should auto-set to 990
    await expect(authenticatedPage.getByLabel("Port")).toHaveValue("990");

    // Fill auth
    await authenticatedPage.getByLabel("Username").fill("ftpsuser");
    await authenticatedPage.getByLabel("Password").fill("ftpspass123");

    // Verify TLS-specific fields appear for FTPS
    await expect(
      authenticatedPage.getByText("TLS Version Floor")
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText("TLS Certificate Policy")
    ).toBeVisible();

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create Connection" })
      .click();

    // Should redirect to detail page
    await expect(authenticatedPage).toHaveURL(/\/connections\/[\w-]+$/, {
      timeout: 10_000,
    });
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Connection created" })
    ).toBeVisible({ timeout: 10_000 });

    await expect(
      authenticatedPage.getByRole("heading", { name: connName })
    ).toBeVisible();

    // Cleanup
    const url = authenticatedPage.url();
    const id = url.split("/connections/")[1];
    if (id) {
      await deleteTestConnection(apiHelper.request, id);
    }
  });

  test("shows Azure Function form fields when protocol is selected", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible({ timeout: 10_000 });

    // Change protocol to Azure Function
    await authenticatedPage
      .getByRole("combobox")
      .filter({ hasText: "SFTP" })
      .click();
    await authenticatedPage
      .getByRole("option", { name: "Azure Function" })
      .click();

    // Port should auto-set to 443
    await expect(authenticatedPage.getByLabel("Port")).toHaveValue("443");

    // Azure Function Settings card should appear
    await expect(
      authenticatedPage.getByText("Azure Function Settings")
    ).toBeVisible();

    // Azure-specific fields should be visible
    await expect(authenticatedPage.getByLabel("Master Key")).toBeVisible();
    await expect(authenticatedPage.getByLabel("Client Secret")).toBeVisible();
    await expect(authenticatedPage.getByLabel("Tenant ID", { exact: true })).toBeVisible();
    await expect(authenticatedPage.getByLabel("Client ID", { exact: true })).toBeVisible();
    await expect(authenticatedPage.getByLabel("Workspace ID")).toBeVisible();

    // Fill fields to verify they accept input
    await authenticatedPage.getByLabel("Name", { exact: true }).fill("e2e-azure-test");
    await authenticatedPage.getByLabel("Host").fill("myapp.azurewebsites.net");
    await authenticatedPage.getByLabel("Master Key").fill("test-master-key-123");
    await authenticatedPage.getByLabel("Client Secret").fill("test-client-secret-456");
    await authenticatedPage.getByLabel("Tenant ID", { exact: true }).fill("12345678-abcd-1234-abcd-123456789012");
    await authenticatedPage.getByLabel("Client ID", { exact: true }).fill("87654321-dcba-4321-dcba-210987654321");
    await authenticatedPage.getByLabel("Workspace ID").fill("abcdef12-3456-7890-abcd-ef1234567890");

    // Verify field values were set
    await expect(authenticatedPage.getByLabel("Host")).toHaveValue("myapp.azurewebsites.net");
    await expect(authenticatedPage.getByLabel("Tenant ID", { exact: true })).toHaveValue("12345678-abcd-1234-abcd-123456789012");
  });

  test("test connection button shows result", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const conn = await createTestConnection(apiHelper.request, {
      name: `e2e-testconn-${Date.now().toString(36)}`,
      host: "nonexistent.test.example.com",
    });

    try {
      await authenticatedPage.goto(`/connections/${conn.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: conn.name })
      ).toBeVisible();

      // Click Test Connection button
      await authenticatedPage
        .getByRole("button", { name: "Test Connection" })
        .click();

      // Should show the test result card (connected or failed - both are valid)
      await expect(
        authenticatedPage.getByText("Test Result")
      ).toBeVisible({ timeout: 30_000 });
    } finally {
      await deleteTestConnection(apiHelper.request, conn.id);
    }
  });

  test("creates SFTP connection with SSH key auth method", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();

    // Password field should be visible by default (auth method = Password)
    await expect(
      authenticatedPage.getByLabel("Password")
    ).toBeVisible();

    // Change auth method to SSH Key
    const authMethodTrigger = authenticatedPage
      .getByRole("combobox")
      .filter({ hasText: "Password" });
    await authMethodTrigger.click();
    await authenticatedPage
      .getByRole("option", { name: "SSH Key", exact: true })
      .click();

    // Password field should be hidden, SSH Key field should appear
    await expect(
      authenticatedPage.getByLabel("Password")
    ).not.toBeVisible();
    await expect(
      authenticatedPage.getByText("SSH Key", { exact: true }).last()
    ).toBeVisible();

    // Change auth method to Password + SSH Key — both fields should appear
    await authenticatedPage
      .getByRole("combobox")
      .filter({ hasText: "SSH Key" })
      .click();
    await authenticatedPage
      .getByRole("option", { name: "Password + SSH Key" })
      .click();

    await expect(
      authenticatedPage.getByLabel("Password")
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText("SSH Key", { exact: true }).last()
    ).toBeVisible();
  });

  test("SFTP form shows host key policy setting", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();

    // Protocol defaults to SFTP, so SFTP Settings card should be visible
    await expect(
      authenticatedPage.getByText("SFTP Settings")
    ).toBeVisible();

    // Host Key Policy select should be visible
    await expect(
      authenticatedPage.getByText("Host Key Policy")
    ).toBeVisible();

    // Click the Host Key Policy select to verify options
    const hostKeyTrigger = authenticatedPage
      .locator("form")
      .getByRole("combobox")
      .filter({ hasText: /Trust on First Use/ });
    await hostKeyTrigger.click();

    // Verify options exist
    await expect(
      authenticatedPage.getByRole("option", { name: "Trust on First Use" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByRole("option", { name: "Always Trust" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByRole("option", { name: "Manual" })
    ).toBeVisible();

    // Select "Always Trust"
    await authenticatedPage
      .getByRole("option", { name: "Always Trust" })
      .click();
  });

  test("FTPS form shows TLS settings", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();

    // Change protocol to FTPS
    await authenticatedPage
      .getByRole("combobox")
      .filter({ hasText: "SFTP" })
      .click();
    await authenticatedPage
      .getByRole("option", { name: "FTPS", exact: true })
      .click();

    // FTPS Settings card should appear with TLS fields
    await expect(
      authenticatedPage.getByText("FTPS Settings")
    ).toBeVisible();

    // TLS Version Floor select
    await expect(
      authenticatedPage.getByText("TLS Version Floor")
    ).toBeVisible();

    // TLS Certificate Policy select
    await expect(
      authenticatedPage.getByText("TLS Certificate Policy")
    ).toBeVisible();

    // Click TLS Version Floor and verify options
    const tlsFloorTrigger = authenticatedPage
      .locator("form")
      .getByRole("combobox")
      .filter({ hasText: /TLS 1\.2/ });
    await tlsFloorTrigger.click();
    await expect(
      authenticatedPage.getByRole("option", { name: "TLS 1.2" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByRole("option", { name: "TLS 1.3" })
    ).toBeVisible();
    await authenticatedPage
      .getByRole("option", { name: "TLS 1.3" })
      .click();

    // Click TLS Certificate Policy and verify options
    const tlsCertTrigger = authenticatedPage
      .locator("form")
      .getByRole("combobox")
      .filter({ hasText: /System Trust/ });
    await tlsCertTrigger.click();
    await expect(
      authenticatedPage.getByRole("option", { name: "Insecure" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByRole("option", { name: "Pinned Thumbprint" })
    ).toBeVisible();
  });

  test("FTP form shows passive mode toggle", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();

    // Change protocol to FTP
    await authenticatedPage
      .getByRole("combobox")
      .filter({ hasText: "SFTP" })
      .click();
    await authenticatedPage
      .getByRole("option", { name: "FTP", exact: true })
      .click();

    // FTP Settings card should appear
    await expect(
      authenticatedPage.getByText("FTP Settings")
    ).toBeVisible();

    // Passive Mode label and switch should be visible
    await expect(
      authenticatedPage.locator("label", { hasText: "Passive Mode" })
    ).toBeVisible();
    await expect(
      authenticatedPage.locator("p", { hasText: "Use passive mode for data connections" })
    ).toBeVisible();

    // The switch should exist (default is on)
    const passiveSwitch = authenticatedPage.locator("main").getByRole("switch");
    await expect(passiveSwitch).toBeVisible();
    await expect(passiveSwitch).toBeChecked();

    // Toggle it off
    await passiveSwitch.click();
    await expect(passiveSwitch).not.toBeChecked();
  });

  test("connection group field works", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const connName = `e2e-group-${suffix}`;
    const groupName = `test-group-${suffix}`;

    await authenticatedPage.goto("/connections/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Connection" })
    ).toBeVisible();

    // Fill Name and Group
    await authenticatedPage.getByLabel("Name", { exact: true }).fill(connName);
    await authenticatedPage.getByLabel("Group").fill(groupName);

    // Fill required fields
    await authenticatedPage.getByLabel("Host").fill("group.test.example.com");
    await authenticatedPage.getByLabel("Username").fill("groupuser");
    await authenticatedPage.getByLabel("Password").fill("grouppass123");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create Connection" })
      .click();

    // Should redirect to detail page
    await expect(authenticatedPage).toHaveURL(/\/connections\/[\w-]+$/, {
      timeout: 10_000,
    });
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Connection created" })
    ).toBeVisible({ timeout: 10_000 });

    // Group badge should be visible on detail page
    await expect(
      authenticatedPage.getByText(groupName)
    ).toBeVisible();

    // Cleanup
    const url = authenticatedPage.url();
    const id = url.split("/connections/")[1];
    if (id) {
      await deleteTestConnection(apiHelper.request, id);
    }
  });

  test("pagination appears with many connections", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const conns: Array<{ id: string; name: string }> = [];

    try {
      // Create 11 connections to exceed page size of 10
      for (let i = 0; i < 11; i++) {
        const conn = await createTestConnection(apiHelper.request, {
          name: `e2e-page-${suffix}-${String(i).padStart(2, "0")}`,
        });
        conns.push(conn);
      }

      await authenticatedPage.goto("/connections");
      await authenticatedPage.waitForURL("**/connections");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Connections", exact: true }).first()
      ).toBeVisible();

      // Wait for data to load - any of the created connections should appear
      await expect(
        authenticatedPage.locator("table tbody tr").first()
      ).toBeVisible({ timeout: 15_000 });

      // Pagination controls should appear
      await expect(
        authenticatedPage.locator("main").getByRole("button", { name: "Next", exact: true })
      ).toBeVisible({ timeout: 10_000 });
      await expect(
        authenticatedPage.getByText(/Page \d+ of \d+/)
      ).toBeVisible();
    } finally {
      for (const conn of conns) {
        await deleteTestConnection(apiHelper.request, conn.id);
      }
    }
  });

  test("detail page shows all connection properties", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const conn = await createTestConnection(apiHelper.request, {
      name: `e2e-full-detail-${suffix}`,
      protocol: "sftp",
      host: "full-detail.example.com",
      port: 2222,
      username: "fulldetailuser",
    });

    try {
      await authenticatedPage.goto(`/connections/${conn.id}`);

      // Heading shows connection name
      await expect(
        authenticatedPage.getByRole("heading", { name: conn.name })
      ).toBeVisible();

      // Protocol badge
      await expect(
        authenticatedPage.locator("main").getByText("SFTP", { exact: false }).first()
      ).toBeVisible();

      // Host:port
      await expect(
        authenticatedPage.getByText("full-detail.example.com:2222")
      ).toBeVisible();

      // Username
      await expect(
        authenticatedPage.getByText("fulldetailuser")
      ).toBeVisible();

      // Auth Method label
      await expect(
        authenticatedPage.getByText("Auth Method")
      ).toBeVisible();

      // Connection Info card
      await expect(
        authenticatedPage.getByText("Connection Info")
      ).toBeVisible();

      // Settings card for file transfer protocol
      await expect(
        authenticatedPage.locator("main").getByText("Settings", { exact: true })
      ).toBeVisible();

      // Created date is shown
      await expect(
        authenticatedPage.getByText(/Created/)
      ).toBeVisible();

      // Test Connection button
      await expect(
        authenticatedPage.getByRole("button", { name: "Test Connection" })
      ).toBeVisible();

      // Edit button
      await expect(
        authenticatedPage.getByRole("link", { name: "Edit" })
      ).toBeVisible();
    } finally {
      await deleteTestConnection(apiHelper.request, conn.id);
    }
  });
});
