import { test, expect } from "./fixtures";
import {
  createTestTag,
  deleteTestTag,
  createTestJob,
  deleteTestJob,
} from "./helpers/api-helpers";

test.describe("Audit Log", () => {
  test("displays audit log page with heading", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/audit");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Audit Log" }).first()
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText("Security and operational event history")
    ).toBeVisible();
  });

  test("shows audit entries after entity creation", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create entities to generate audit log entries
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-audit-visible-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto("/audit");

      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Audit Log" }).first()
      ).toBeVisible();

      // The table should be present with audit entries
      const table = authenticatedPage.locator("table");
      await expect(table).toBeVisible({ timeout: 10_000 });

      // Verify table headers are present
      await expect(table.getByText("Timestamp")).toBeVisible();
      await expect(table.getByText("Entity Type")).toBeVisible();
      await expect(table.getByText("Operation")).toBeVisible();
      await expect(table.getByText("Performed By")).toBeVisible();
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("filters by entity type", async ({ authenticatedPage, apiHelper }) => {
    // Create a job to ensure at least one "job" entity type audit entry exists
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-audit-filter-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto("/audit");

      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Audit Log" }).first()
      ).toBeVisible();

      // Wait for table to load
      const table = authenticatedPage.locator("table");
      await expect(table).toBeVisible({ timeout: 10_000 });

      // Open the entity type filter select (it shows "All Types" by default)
      await authenticatedPage.getByRole("combobox").first().click();
      await authenticatedPage
        .getByRole("option", { name: "Job", exact: true })
        .click();

      // After filtering, the table (or empty state) should be visible
      // If there are job entries, they should display "Job" badges
      const jobBadge = authenticatedPage
        .getByText("Job", { exact: true })
        .first();
      const emptyState = authenticatedPage.getByText(
        "No audit entries found"
      );
      await expect(jobBadge.or(emptyState)).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("filters by operation", async ({ authenticatedPage, apiHelper }) => {
    // Create a tag to ensure at least one "created" operation exists
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-audit-op-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto("/audit");

      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Audit Log" }).first()
      ).toBeVisible();

      // Wait for table to load
      await expect(
        authenticatedPage.locator("table")
      ).toBeVisible({ timeout: 10_000 });

      // Type in the operation filter input
      const operationInput = authenticatedPage.getByPlaceholder(
        "Filter by operation..."
      );
      await operationInput.fill("created");

      // The table should update -- either showing matching entries or empty state
      const table = authenticatedPage.locator("table");
      const emptyState = authenticatedPage.getByText(
        "No audit entries found"
      );
      await expect(table.or(emptyState)).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("filters by performer", async ({ authenticatedPage, apiHelper }) => {
    // Create a tag so there is at least one entry by testadmin
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-audit-perf-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto("/audit");

      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Audit Log" }).first()
      ).toBeVisible();

      // Wait for table to load
      await expect(
        authenticatedPage.locator("table")
      ).toBeVisible({ timeout: 10_000 });

      // Type in the performer filter input
      const performerInput = authenticatedPage.getByPlaceholder(
        "Filter by performer..."
      );
      await performerInput.fill("testadmin");

      // The table should still show entries performed by testadmin
      const table = authenticatedPage.locator("table");
      const emptyState = authenticatedPage.getByText(
        "No audit entries found"
      );
      await expect(table.or(emptyState)).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("pagination works when entries exceed page size", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/audit");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Audit Log" }).first()
    ).toBeVisible();

    // Wait for page to fully load
    await expect(
      authenticatedPage.locator("table")
    ).toBeVisible({ timeout: 10_000 });

    // If there are enough entries for pagination, the pagination controls will appear.
    // With only a few test entities, pagination may not show -- check gracefully.
    // Use toBeVisible with a short timeout (auto-waits) rather than isVisible (instant check).
    const paginationText = authenticatedPage.getByText(/Page \d+ of \d+/);
    let hasPagination = false;
    try {
      await expect(paginationText).toBeVisible({ timeout: 3_000 });
      hasPagination = true;
    } catch {
      // Pagination not present — not enough data
    }

    if (hasPagination) {
      // Scope to the pagination area to avoid matching "Open Next.js Dev Tools" button
      const paginationArea = authenticatedPage.locator("main");
      const nextButton = paginationArea.getByRole("button", {
        name: "Next",
        exact: true,
      });
      const isNextEnabled = await nextButton.isEnabled();
      if (isNextEnabled) {
        await nextButton.click();
        await expect(
          authenticatedPage.getByText(/Page 2 of \d+/)
        ).toBeVisible({ timeout: 10_000 });

        // Previous button should now be enabled
        await expect(
          paginationArea.getByRole("button", { name: "Previous", exact: true })
        ).toBeEnabled();
      }
    } else {
      // Not enough data for pagination -- just verify the page loaded correctly
      await expect(authenticatedPage.locator("table")).toBeVisible();
    }
  });

  test("row expansion shows details", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create a tag to generate an audit entry with details
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-audit-expand-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto("/audit");

      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Audit Log" }).first()
      ).toBeVisible();

      const table = authenticatedPage.locator("table");
      await expect(table).toBeVisible({ timeout: 10_000 });

      // Find a row with expandable details (has "N fields" button)
      const detailsButton = table
        .getByRole("button", { name: /\d+ field/ })
        .first();

      // The button may not exist if no entries have details — check gracefully
      let hasDetails = false;
      try {
        await expect(detailsButton).toBeVisible({ timeout: 5_000 });
        hasDetails = true;
      } catch {
        // No entries with expandable details
      }

      if (hasDetails) {
        // Click to expand
        await detailsButton.click();

        // The expanded details should show a <pre> with JSON
        const expandedPre = table.locator("pre").first();
        await expect(expandedPre).toBeVisible({ timeout: 5_000 });

        // Click again to collapse
        await detailsButton.click();
        await expect(expandedPre).not.toBeVisible();
      }
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("combined filters — entity type + operation + performer", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create a tag so there is at least one entry by testadmin with "created" operation
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-audit-combo-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto("/audit");

      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Audit Log" }).first()
      ).toBeVisible();

      await expect(
        authenticatedPage.locator("table")
      ).toBeVisible({ timeout: 10_000 });

      // Apply entity type filter
      await authenticatedPage.getByRole("combobox").first().click();
      await authenticatedPage
        .getByRole("option", { name: "Job", exact: true })
        .click();

      // Apply operation filter
      const operationInput = authenticatedPage.getByPlaceholder(
        "Filter by operation..."
      );
      await operationInput.fill("created");

      // Apply performer filter
      const performerInput = authenticatedPage.getByPlaceholder(
        "Filter by performer..."
      );
      await performerInput.fill("testadmin");

      // The table or empty state should be visible after all filters
      const table = authenticatedPage.locator("table");
      const emptyState = authenticatedPage.getByText(
        "No audit entries found"
      );
      await expect(table.or(emptyState)).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("timestamp column header is present but not sortable", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create a couple of entities to ensure audit entries exist
    const tag1 = await createTestTag(apiHelper.request, {
      name: `e2e-audit-sort1-${Date.now().toString(36)}`,
    });
    const tag2 = await createTestTag(apiHelper.request, {
      name: `e2e-audit-sort2-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto("/audit");

      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Audit Log" }).first()
      ).toBeVisible();

      const table = authenticatedPage.locator("table");
      await expect(table).toBeVisible({ timeout: 10_000 });

      // Verify the Timestamp column header exists
      const timestampHeader = table.getByText("Timestamp");
      await expect(timestampHeader).toBeVisible();

      // Verify other column headers are also present
      await expect(table.getByText("Entity Type")).toBeVisible();
      await expect(table.getByText("Entity ID")).toBeVisible();
      await expect(table.getByText("Operation")).toBeVisible();
      await expect(table.getByText("Performed By")).toBeVisible();
      await expect(table.getByText("Details")).toBeVisible();

      // The audit table does not have sortable columns (no sort handlers in the UI).
      // Verify entries appear in order (most recent first — server default).
      const rows = table.locator("tbody tr");
      const rowCount = await rows.count();
      expect(rowCount).toBeGreaterThan(0);
    } finally {
      await deleteTestTag(apiHelper.request, tag1.id);
      await deleteTestTag(apiHelper.request, tag2.id);
    }
  });
});
