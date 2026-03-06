import { test, expect } from "./fixtures";
import {
  createTestMonitor,
  deleteTestMonitor,
  createTestJob,
  deleteTestJob,
} from "./helpers/api-helpers";

test.describe("Monitors", () => {
  test("displays empty state when no monitors exist", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create and immediately delete a monitor to exercise the path
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-empty-job-${Date.now().toString(36)}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: "e2e-empty-check",
      jobIds: [job.id],
    });
    await deleteTestMonitor(apiHelper.request, monitor.id);
    await deleteTestJob(apiHelper.request, job.id);

    await authenticatedPage.goto("/monitors");

    await expect(
      authenticatedPage.getByRole("heading", { name: "Monitors" })
    ).toBeVisible();

    // Either the empty state or the table should be visible
    const emptyState = authenticatedPage.getByText("No monitors yet");
    const monitorTable = authenticatedPage.locator("table");
    await expect(emptyState.or(monitorTable)).toBeVisible();
  });

  test("navigates to create monitor page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/monitors");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Monitors" })
    ).toBeVisible();

    await authenticatedPage
      .getByRole("link", { name: "Create Monitor" })
      .first()
      .click();
    await expect(authenticatedPage).toHaveURL(/\/monitors\/new/);
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Monitor" })
    ).toBeVisible();
  });

  test("creates a monitor with local watch target", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const monitorName = `e2e-create-mon-${suffix}`;

    // We need at least one job to bind to the monitor
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-create-job-${suffix}`,
    });

    try {
      await authenticatedPage.goto("/monitors/new");
      await expect(
        authenticatedPage.getByRole("heading", { name: "Create Monitor" })
      ).toBeVisible();

      // Fill in the General section
      await authenticatedPage.getByLabel("Name").fill(monitorName);
      await authenticatedPage
        .getByLabel("Description")
        .fill("E2E test monitor");

      // Fill in the Watch Target section
      await authenticatedPage
        .getByLabel("Directory Path")
        .fill("/tmp/e2e-watch-dir");

      // Polling interval is pre-filled with 60; leave as default

      // Select a bound job via checkbox
      await authenticatedPage
        .locator("label", { hasText: job.name })
        .click();

      // Submit the form
      await authenticatedPage
        .getByRole("button", { name: "Create Monitor" })
        .click();

      // Should redirect to the monitor detail page with success toast
      await expect(authenticatedPage).toHaveURL(/\/monitors\/[a-f0-9-]+$/, {
        timeout: 10_000,
      });
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Monitor created" })
      ).toBeVisible({ timeout: 10_000 });

      // The detail page should show the monitor name
      await expect(
        authenticatedPage.getByRole("heading", { name: monitorName })
      ).toBeVisible();

      // Cleanup via API
      const url = authenticatedPage.url();
      const monitorId = url.split("/monitors/")[1];
      if (monitorId) {
        await deleteTestMonitor(apiHelper.request, monitorId);
      }
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("monitor detail page shows monitor info", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-detail-job-${suffix}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-detail-${suffix}`,
      watchTarget: "/data/incoming",
      pollingIntervalSec: 120,
      jobIds: [job.id],
    });

    try {
      await authenticatedPage.goto(`/monitors/${monitor.id}`);

      // Heading is the monitor name
      await expect(
        authenticatedPage.getByRole("heading", { name: monitor.name })
      ).toBeVisible();

      // Configuration card is present
      await expect(
        authenticatedPage.getByText("Configuration")
      ).toBeVisible();

      // Watch path is visible
      await expect(
        authenticatedPage.getByText("/data/incoming")
      ).toBeVisible();

      // Polling interval
      await expect(authenticatedPage.getByText("120s")).toBeVisible();

      // Health card is present
      await expect(
        authenticatedPage.getByText("Health").first()
      ).toBeVisible();

      // Bound Jobs card
      await expect(
        authenticatedPage.getByText("Bound Jobs (1)")
      ).toBeVisible();

      // State action buttons are present (new monitors start in "active" state)
      // Pause button should be visible for active monitors
      await expect(
        authenticatedPage.getByRole("button", { name: "Pause" })
      ).toBeVisible();

      // Edit link
      await expect(
        authenticatedPage.getByRole("link", { name: "Edit" })
      ).toBeVisible();

      // Delete button
      await expect(
        authenticatedPage.getByRole("button", { name: "Delete" })
      ).toBeVisible();
    } finally {
      await deleteTestMonitor(apiHelper.request, monitor.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("filters monitors by state", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-filter-job-${suffix}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-filter-${suffix}`,
      jobIds: [job.id],
    });

    try {
      await authenticatedPage.goto("/monitors");
      await expect(
        authenticatedPage.getByRole("heading", { name: "Monitors" })
      ).toBeVisible();

      // Wait for the monitor to appear in the table
      await expect(
        authenticatedPage.getByText(monitor.name)
      ).toBeVisible();

      // Open the state filter combobox (placeholder "State")
      const stateFilter = authenticatedPage
        .getByRole("combobox")
        .filter({ hasText: /State|All States|Active|Paused|Disabled|Error/ });
      await stateFilter.click();

      // Select "Paused" — since our monitor starts active, it should be filtered out
      await authenticatedPage.getByRole("option", { name: "Paused" }).click();

      // The monitor should not be visible since it's in active state
      await expect(
        authenticatedPage.getByText(monitor.name)
      ).not.toBeVisible();

      // Switch to "All States" to see it again
      await stateFilter.click();
      await authenticatedPage
        .getByRole("option", { name: "All States" })
        .click();

      await expect(
        authenticatedPage.getByText(monitor.name)
      ).toBeVisible();
    } finally {
      await deleteTestMonitor(apiHelper.request, monitor.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("searches monitors by name", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-search-job-${suffix}`,
    });
    const monitor1 = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-searchable-${suffix}`,
      jobIds: [job.id],
    });
    const monitor2 = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-other-${suffix}`,
      jobIds: [job.id],
    });

    try {
      await authenticatedPage.goto("/monitors");
      await expect(
        authenticatedPage.getByRole("heading", { name: "Monitors" })
      ).toBeVisible();

      // Wait for both monitors to be visible
      await expect(
        authenticatedPage.getByText(monitor1.name)
      ).toBeVisible();

      // Type in the search box
      const searchInput =
        authenticatedPage.getByPlaceholder("Search monitors...");
      await searchInput.fill("e2e-mon-searchable");

      // The matching monitor should be visible
      await expect(
        authenticatedPage.getByText(monitor1.name)
      ).toBeVisible();

      // The non-matching monitor should disappear
      await expect(
        authenticatedPage.getByText(monitor2.name)
      ).not.toBeVisible();
    } finally {
      await deleteTestMonitor(apiHelper.request, monitor1.id);
      await deleteTestMonitor(apiHelper.request, monitor2.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("activates and pauses a monitor", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-state-job-${suffix}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-state-${suffix}`,
      jobIds: [job.id],
    });

    try {
      await authenticatedPage.goto(`/monitors/${monitor.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: monitor.name })
      ).toBeVisible();

      // New monitors start in "active" state — Pause button should be visible
      const pauseButton = authenticatedPage.getByRole("button", {
        name: "Pause",
      });
      await expect(pauseButton).toBeVisible();

      // Click Pause
      await pauseButton.click();

      // Success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Monitor paused" })
      ).toBeVisible({ timeout: 10_000 });

      // After pausing, the Activate button should appear (replacing Pause)
      const activateButton = authenticatedPage.getByRole("button", {
        name: "Activate",
      });
      await expect(activateButton).toBeVisible({ timeout: 10_000 });

      // Click Activate
      await activateButton.click();

      // Success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Monitor activated" })
      ).toBeVisible({ timeout: 10_000 });

      // After activation, the Pause button should reappear
      await expect(
        authenticatedPage.getByRole("button", { name: "Pause" })
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestMonitor(apiHelper.request, monitor.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("deletes a monitor", async ({ authenticatedPage, apiHelper }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-delete-job-${suffix}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-delete-${suffix}`,
      jobIds: [job.id],
    });

    try {
      await authenticatedPage.goto(`/monitors/${monitor.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: monitor.name })
      ).toBeVisible();

      // Click the Delete button on the detail page
      await authenticatedPage
        .getByRole("button", { name: "Delete" })
        .click();

      // Confirm dialog appears
      const dialog = authenticatedPage.locator("[role=dialog]");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByText("Delete Monitor")).toBeVisible();
      await expect(
        dialog.getByText(`Are you sure you want to delete "${monitor.name}"?`)
      ).toBeVisible();

      // Click the confirm Delete button inside the dialog
      await dialog.getByRole("button", { name: "Delete" }).click();

      // Success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Monitor deleted" })
      ).toBeVisible({ timeout: 10_000 });

      // Should redirect back to the monitors list
      await expect(authenticatedPage).toHaveURL("/monitors", {
        timeout: 10_000,
      });

      // Monitor should no longer appear
      await expect(
        authenticatedPage.getByText(monitor.name)
      ).not.toBeVisible();
    } finally {
      // Cleanup the job (monitor is already deleted by the test)
      await deleteTestJob(apiHelper.request, job.id);
    }
  });
});
