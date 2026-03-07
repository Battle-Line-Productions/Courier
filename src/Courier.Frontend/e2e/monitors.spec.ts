import { test, expect } from "./fixtures";
import {
  createTestMonitor,
  deleteTestMonitor,
  createTestJob,
  deleteTestJob,
  createTestConnection,
  deleteTestConnection,
  disableMonitor,
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
      authenticatedPage.locator("main").getByRole("heading", { name: "Monitors" }).first()
    ).toBeVisible({ timeout: 10_000 });

    // Either the empty state or the table should be visible
    const emptyState = authenticatedPage.getByText("No monitors yet");
    const monitorTable = authenticatedPage.locator("table");
    await expect(emptyState.or(monitorTable)).toBeVisible({ timeout: 10_000 });
  });

  test("navigates to create monitor page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/monitors");
    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Monitors" }).first()
    ).toBeVisible({ timeout: 10_000 });

    await authenticatedPage
      .getByRole("link", { name: "Create Monitor" })
      .first()
      .click();
    await expect(authenticatedPage).toHaveURL(/\/monitors\/new/, { timeout: 10_000 });
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Monitor" })
    ).toBeVisible({ timeout: 10_000 });
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
      ).toBeVisible({ timeout: 10_000 });

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
      await expect(
        authenticatedPage.locator("label", { hasText: job.name })
      ).toBeVisible({ timeout: 10_000 });
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
      ).toBeVisible({ timeout: 10_000 });

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
        authenticatedPage.locator("main").getByRole("heading", { name: "Monitors" }).first()
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
        authenticatedPage.locator("main").getByRole("heading", { name: "Monitors" }).first()
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

  test("edits a monitor", async ({ authenticatedPage, apiHelper }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-edit-job-${suffix}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-edit-${suffix}`,
      jobIds: [job.id],
    });

    try {
      await authenticatedPage.goto(`/monitors/${monitor.id}/edit`);
      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit Monitor" })
      ).toBeVisible({ timeout: 10_000 });

      // Change the name
      const nameInput = authenticatedPage.getByLabel("Name");
      await nameInput.clear();
      const updatedName = `e2e-mon-edited-${suffix}`;
      await nameInput.fill(updatedName);

      // Change the description
      const descInput = authenticatedPage.getByLabel("Description");
      await descInput.clear();
      await descInput.fill("Updated E2E description");

      // Submit the form
      await authenticatedPage
        .getByRole("button", { name: "Update Monitor" })
        .click();

      // Should redirect to detail page with success toast
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/monitors/${monitor.id}$`),
        { timeout: 10_000 }
      );
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Monitor updated" })
      ).toBeVisible({ timeout: 10_000 });

      // Detail page should show updated name
      await expect(
        authenticatedPage.getByRole("heading", { name: updatedName })
      ).toBeVisible();

      // Description should show updated text
      await expect(
        authenticatedPage.getByText("Updated E2E description")
      ).toBeVisible();
    } finally {
      await deleteTestMonitor(apiHelper.request, monitor.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("trigger event switches on create form", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-triggers-job-${suffix}`,
    });

    try {
      await authenticatedPage.goto("/monitors/new");
      await expect(
        authenticatedPage.getByRole("heading", { name: "Create Monitor" })
      ).toBeVisible({ timeout: 10_000 });

      // Fill required fields
      await authenticatedPage
        .getByLabel("Name")
        .fill(`e2e-mon-triggers-${suffix}`);
      await authenticatedPage
        .getByLabel("Directory Path")
        .fill("/tmp/e2e-triggers");

      // "File Created" switch should be on by default
      // The trigger events section has labeled switches

      // Toggle "File Modified" on
      const fileModifiedSwitch = authenticatedPage
        .locator(".flex.items-center.justify-between", { hasText: "File Modified" })
        .getByRole("switch");
      await fileModifiedSwitch.click();

      // Toggle "File Exists" on
      const fileExistsSwitch = authenticatedPage
        .locator(".flex.items-center.justify-between", { hasText: "File Exists" })
        .getByRole("switch");
      await fileExistsSwitch.click();

      // Verify the switches are checked
      await expect(fileModifiedSwitch).toBeChecked();
      await expect(fileExistsSwitch).toBeChecked();

      // Select a bound job
      await authenticatedPage
        .locator("label", { hasText: job.name })
        .click();

      // Submit
      await authenticatedPage
        .getByRole("button", { name: "Create Monitor" })
        .click();

      // Should redirect to detail page
      await expect(authenticatedPage).toHaveURL(/\/monitors\/[a-f0-9-]+$/, {
        timeout: 10_000,
      });

      // Detail page should show all three trigger events
      await expect(
        authenticatedPage.getByText("File Created, File Modified, File Exists")
      ).toBeVisible({ timeout: 10_000 });

      // Cleanup
      const url = authenticatedPage.url();
      const monitorId = url.split("/monitors/")[1];
      if (monitorId) {
        await deleteTestMonitor(apiHelper.request, monitorId);
      }
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("polling config on create form", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-polling-job-${suffix}`,
    });

    try {
      await authenticatedPage.goto("/monitors/new");
      await expect(
        authenticatedPage.getByRole("heading", { name: "Create Monitor" })
      ).toBeVisible({ timeout: 10_000 });

      // Fill required fields
      await authenticatedPage
        .getByLabel("Name")
        .fill(`e2e-mon-polling-${suffix}`);
      await authenticatedPage
        .getByLabel("Directory Path")
        .fill("/tmp/e2e-polling");

      // Change polling interval from default 60 to 30
      const pollingInput = authenticatedPage.getByLabel(
        "Polling Interval (seconds)"
      );
      await pollingInput.clear();
      await pollingInput.fill("30");

      // Enable batch mode
      const batchSwitch = authenticatedPage
        .locator(".flex.items-center.justify-between", { hasText: "Batch Mode" })
        .getByRole("switch");
      await batchSwitch.click();
      await expect(batchSwitch).toBeChecked();

      // Set max consecutive failures
      const maxFailuresInput = authenticatedPage.getByLabel(
        "Max Consecutive Failures"
      );
      await maxFailuresInput.clear();
      await maxFailuresInput.fill("3");

      // Select a bound job
      await authenticatedPage
        .locator("label", { hasText: job.name })
        .click();

      // Submit
      await authenticatedPage
        .getByRole("button", { name: "Create Monitor" })
        .click();

      // Should redirect to detail page
      await expect(authenticatedPage).toHaveURL(/\/monitors\/[a-f0-9-]+$/, {
        timeout: 10_000,
      });

      // Verify polling interval on detail page
      await expect(authenticatedPage.getByText("30s")).toBeVisible({
        timeout: 10_000,
      });

      // Verify batch mode is enabled
      await expect(
        authenticatedPage
          .locator("dd")
          .filter({ hasText: "Enabled" })
      ).toBeVisible();

      // Cleanup
      const url = authenticatedPage.url();
      const monitorId = url.split("/monitors/")[1];
      if (monitorId) {
        await deleteTestMonitor(apiHelper.request, monitorId);
      }
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("multiple bound jobs", async ({ authenticatedPage, apiHelper }) => {
    const suffix = Date.now().toString(36);
    const job1 = await createTestJob(apiHelper.request, {
      name: `e2e-mon-bind1-${suffix}`,
    });
    const job2 = await createTestJob(apiHelper.request, {
      name: `e2e-mon-bind2-${suffix}`,
    });

    try {
      await authenticatedPage.goto("/monitors/new");
      await expect(
        authenticatedPage.getByRole("heading", { name: "Create Monitor" })
      ).toBeVisible({ timeout: 10_000 });

      // Fill required fields
      await authenticatedPage
        .getByLabel("Name")
        .fill(`e2e-mon-multi-${suffix}`);
      await authenticatedPage
        .getByLabel("Directory Path")
        .fill("/tmp/e2e-multi");

      // Select both jobs via checkboxes
      await authenticatedPage
        .locator("label", { hasText: job1.name })
        .click();
      await authenticatedPage
        .locator("label", { hasText: job2.name })
        .click();

      // Submit
      await authenticatedPage
        .getByRole("button", { name: "Create Monitor" })
        .click();

      // Should redirect to detail page
      await expect(authenticatedPage).toHaveURL(/\/monitors\/[a-f0-9-]+$/, {
        timeout: 10_000,
      });

      // Detail page should show "Bound Jobs (2)"
      await expect(
        authenticatedPage.getByText("Bound Jobs (2)")
      ).toBeVisible({ timeout: 10_000 });

      // Both job names should appear
      await expect(
        authenticatedPage.getByText(job1.name)
      ).toBeVisible();
      await expect(
        authenticatedPage.getByText(job2.name)
      ).toBeVisible();

      // Cleanup
      const url = authenticatedPage.url();
      const monitorId = url.split("/monitors/")[1];
      if (monitorId) {
        await deleteTestMonitor(apiHelper.request, monitorId);
      }
    } finally {
      await deleteTestJob(apiHelper.request, job1.id);
      await deleteTestJob(apiHelper.request, job2.id);
    }
  });

  test("disabled monitor shows correct state", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-disabled-job-${suffix}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-disabled-${suffix}`,
      jobIds: [job.id],
    });

    try {
      // Disable the monitor via API
      await disableMonitor(apiHelper.request, monitor.id);

      await authenticatedPage.goto(`/monitors/${monitor.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: monitor.name })
      ).toBeVisible({ timeout: 10_000 });

      // Should show "disabled" state badge
      await expect(
        authenticatedPage.locator("[data-slot='badge']", { hasText: "disabled" })
      ).toBeVisible({ timeout: 10_000 });

      // Disable button should NOT be visible (already disabled)
      await expect(
        authenticatedPage.getByRole("button", { name: "Disable" })
      ).not.toBeVisible();

      // Activate button should be visible
      await expect(
        authenticatedPage.getByRole("button", { name: "Activate" })
      ).toBeVisible();
    } finally {
      await deleteTestMonitor(apiHelper.request, monitor.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("acknowledge error button visible for error state", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-ack-job-${suffix}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-ack-${suffix}`,
      jobIds: [job.id],
    });

    try {
      // In non-error state, Acknowledge button should NOT be visible
      await authenticatedPage.goto(`/monitors/${monitor.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: monitor.name })
      ).toBeVisible({ timeout: 10_000 });

      await expect(
        authenticatedPage.getByRole("button", { name: "Acknowledge" })
      ).not.toBeVisible();

      // Verify the state action buttons that ARE visible for active state
      await expect(
        authenticatedPage.getByRole("button", { name: "Pause" })
      ).toBeVisible();
      await expect(
        authenticatedPage.getByRole("button", { name: "Disable" })
      ).toBeVisible();
    } finally {
      await deleteTestMonitor(apiHelper.request, monitor.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("file log section exists on detail page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-filelog-job-${suffix}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-filelog-${suffix}`,
      jobIds: [job.id],
    });

    try {
      await authenticatedPage.goto(`/monitors/${monitor.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: monitor.name })
      ).toBeVisible({ timeout: 10_000 });

      // File Log card title should be visible
      await expect(
        authenticatedPage.getByText("File Log", { exact: true })
      ).toBeVisible();

      // Since no events have been detected, empty message should show
      await expect(
        authenticatedPage.getByText("No file events detected yet.")
      ).toBeVisible();
    } finally {
      await deleteTestMonitor(apiHelper.request, monitor.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("file log table headers present when empty", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-filelog-hdr-job-${suffix}`,
    });
    const monitor = await createTestMonitor(apiHelper.request, {
      name: `e2e-mon-filelog-hdr-${suffix}`,
      jobIds: [job.id],
    });

    try {
      await authenticatedPage.goto(`/monitors/${monitor.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: monitor.name })
      ).toBeVisible({ timeout: 10_000 });

      // The file log card should exist
      await expect(
        authenticatedPage.getByText("File Log", { exact: true })
      ).toBeVisible();

      // With no data, it shows the empty message (no table headers)
      await expect(
        authenticatedPage.getByText("No file events detected yet.")
      ).toBeVisible();

      // The Health card should also be present
      await expect(
        authenticatedPage.getByText("Health", { exact: true }).first()
      ).toBeVisible();

      // Consecutive failures should show 0 for a fresh monitor
      await expect(
        authenticatedPage.getByText("Consecutive Failures")
      ).toBeVisible();
    } finally {
      await deleteTestMonitor(apiHelper.request, monitor.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("list pagination for monitors", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-mon-page-job-${suffix}`,
    });
    const monitors: { id: string; name: string }[] = [];

    try {
      // Create 11 monitors to trigger pagination (page size is 10)
      for (let i = 0; i < 11; i++) {
        const monitor = await createTestMonitor(apiHelper.request, {
          name: `e2e-mon-page-${suffix}-${String(i).padStart(2, "0")}`,
          jobIds: [job.id],
        });
        monitors.push(monitor);
      }

      await authenticatedPage.goto("/monitors");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Monitors" }).first()
      ).toBeVisible();

      // Wait for the table to load
      await expect(
        authenticatedPage.locator("table")
      ).toBeVisible({ timeout: 10_000 });

      // Pagination should be visible
      await expect(
        authenticatedPage.getByText("Page 1 of")
      ).toBeVisible({ timeout: 10_000 });

      // Previous should be disabled on page 1
      const main = authenticatedPage.locator("main");
      const prevButton = main.getByRole("button", {
        name: "Previous",
        exact: true,
      });
      await expect(prevButton).toBeDisabled();

      // Next should be enabled
      const nextButton = main.getByRole("button", {
        name: "Next",
        exact: true,
      });
      await expect(nextButton).toBeEnabled();

      // Navigate to page 2
      await nextButton.click();

      // Should now show page 2
      await expect(
        authenticatedPage.getByText("Page 2 of")
      ).toBeVisible({ timeout: 10_000 });

      // Previous should now be enabled
      await expect(prevButton).toBeEnabled();
    } finally {
      for (const monitor of monitors) {
        await deleteTestMonitor(apiHelper.request, monitor.id);
      }
      await deleteTestJob(apiHelper.request, job.id);
    }
  });
});
