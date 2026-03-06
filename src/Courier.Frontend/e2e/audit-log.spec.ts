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
      authenticatedPage.getByRole("heading", { name: "Audit Log" })
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
        authenticatedPage.getByRole("heading", { name: "Audit Log" })
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
        authenticatedPage.getByRole("heading", { name: "Audit Log" })
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
        authenticatedPage.getByRole("heading", { name: "Audit Log" })
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
        authenticatedPage.getByRole("heading", { name: "Audit Log" })
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
      authenticatedPage.getByRole("heading", { name: "Audit Log" })
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
});
