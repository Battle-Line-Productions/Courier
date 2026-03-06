import { test, expect } from "./fixtures";

test.describe("Dashboard", () => {
  test("displays page heading", async ({ authenticatedPage }) => {
    await expect(
      authenticatedPage.getByRole("heading", { name: "Dashboard" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText("System overview and recent activity.")
    ).toBeVisible();
  });

  test("displays all six summary cards", async ({ authenticatedPage }) => {
    const main = authenticatedPage.getByRole("main");

    const expectedCards = [
      "Jobs",
      "Connections",
      "Monitors",
      "PGP Keys",
      "SSH Keys",
      "Last 24h",
    ];

    for (const label of expectedCards) {
      // Card labels are rendered in uppercase tracking-wide spans
      await expect(
        main.locator(".uppercase.tracking-wide", { hasText: label })
      ).toBeVisible();
    }
  });

  test("summary cards show numeric values", async ({ authenticatedPage }) => {
    const main = authenticatedPage.getByRole("main");

    // Card values use tabular-nums class to distinguish from the page heading
    const values = main.locator(".tabular-nums.text-2xl");
    await expect(values.first()).toBeVisible({ timeout: 10_000 });

    const count = await values.count();
    expect(count).toBe(6);

    for (let i = 0; i < count; i++) {
      const text = await values.nth(i).textContent();
      expect(text?.trim()).toMatch(/^\d+$/);
    }
  });

  test("displays recent executions section", async ({ authenticatedPage }) => {
    const main = authenticatedPage.getByRole("main");

    await expect(
      main.getByText("Recent Executions", { exact: true })
    ).toBeVisible();
    // Fresh DB has no executions
    await expect(main.getByText("No executions yet.")).toBeVisible();
  });

  test("displays active monitors section", async ({ authenticatedPage }) => {
    const main = authenticatedPage.getByRole("main");

    await expect(
      main.getByText("Active Monitors", { exact: true })
    ).toBeVisible();

    // The section should show either the empty state or a list of monitors
    // (other E2E tests may have created monitor data)
    const emptyState = main.getByText("No active monitors.");
    const monitorEntries = main.locator(".rounded-lg.border.p-3");

    await expect(emptyState.or(monitorEntries.first())).toBeVisible();
  });

  test("key expiry section is hidden when no keys exist", async ({
    authenticatedPage,
  }) => {
    const main = authenticatedPage.getByRole("main");
    // KeyExpiryList returns null when no expiring keys
    await expect(
      main.getByText("Key Expiry Warnings")
    ).not.toBeVisible();
  });

  test("sidebar navigation links are visible", async ({
    authenticatedPage,
  }) => {
    const sidebar = authenticatedPage.locator("aside");

    const navLinks = [
      "Dashboard",
      "Jobs",
      "Chains",
      "Connections",
      "Keys",
      "Monitors",
      "Tags",
      "Notifications",
      "Audit",
      "Settings",
    ];

    for (const label of navLinks) {
      await expect(
        sidebar.getByRole("link", { name: label })
      ).toBeVisible();
    }
  });

  test("sidebar navigation works", async ({ authenticatedPage }) => {
    const sidebar = authenticatedPage.locator("aside");

    await sidebar.getByRole("link", { name: "Jobs" }).click();
    await expect(authenticatedPage).toHaveURL(/\/jobs/);

    await sidebar.getByRole("link", { name: "Dashboard" }).click();
    await expect(authenticatedPage).toHaveURL("/");
  });
});
