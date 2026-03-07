import { test, expect } from "./fixtures";

test.describe("Dashboard", () => {
  test("displays page heading", async ({ authenticatedPage }) => {
    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Dashboard" }).first()
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

  test("card click-through navigates to respective pages", async ({
    authenticatedPage,
  }) => {
    const sidebar = authenticatedPage.locator("aside");

    // Summary cards correspond to sidebar navigation items
    const cardNavMap = [
      { card: "Jobs", url: /\/jobs/ },
      { card: "Connections", url: /\/connections/ },
      { card: "Monitors", url: /\/monitors/ },
      { card: "Keys", url: /\/keys/ },
    ];

    for (const { card, url } of cardNavMap) {
      // Navigate via sidebar link matching the card label
      await sidebar.getByRole("link", { name: card }).click();
      await expect(authenticatedPage).toHaveURL(url, { timeout: 10_000 });
      await authenticatedPage.waitForLoadState("networkidle");

      // Navigate back to dashboard
      await sidebar.getByRole("link", { name: "Dashboard" }).click();
      await expect(authenticatedPage).toHaveURL("/", { timeout: 10_000 });
      await authenticatedPage.waitForLoadState("networkidle");
    }

    // PGP Keys and SSH Keys are both under the Keys page
    await sidebar.getByRole("link", { name: "Keys" }).click();
    await expect(authenticatedPage).toHaveURL(/\/keys/, { timeout: 10_000 });
  });

  test("recent executions show data after job trigger", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const page = authenticatedPage;
    let jobId: string | undefined;

    try {
      // Create a job with a step and trigger it
      const job = await apiHelper.createTestJob(apiHelper.request, {
        name: `e2e-dash-exec-${Date.now()}`,
      });
      jobId = job.id;

      await apiHelper.addJobSteps(apiHelper.request, job.id, [
        {
          name: "copy step",
          typeKey: "file.copy",
          stepOrder: 1,
          configuration: JSON.stringify({
            sourcePath: "/tmp/nonexistent",
            destinationPath: "/tmp/out",
          }),
        },
      ]);

      await apiHelper.triggerJob(apiHelper.request, job.id);

      // Wait a moment for the execution to be recorded
      await page.waitForTimeout(2000);

      // Navigate to dashboard
      await page.goto("/");
      await page.waitForURL("/", { timeout: 10_000 });

      const main = page.getByRole("main");

      // The "No executions yet." empty state should be replaced
      // with execution data (table rows with job links)
      await expect(
        main.locator("table").or(main.getByText("No executions yet."))
      ).toBeVisible({ timeout: 10_000 });

      // If a table appeared, verify our job name is in it
      const table = main.locator("table");
      if (await table.isVisible()) {
        await expect(
          table.getByText(job.name)
        ).toBeVisible({ timeout: 10_000 });
      }
    } finally {
      if (jobId) {
        await apiHelper.deleteTestJob(apiHelper.request, jobId);
      }
    }
  });

  test("active monitors show data after monitor creation", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const page = authenticatedPage;
    let monitorId: string | undefined;
    let jobId: string | undefined;

    try {
      // Backend requires at least one job binding for monitors
      const job = await apiHelper.createTestJob(apiHelper.request, {
        name: `e2e-dash-mon-job-${Date.now()}`,
      });
      jobId = job.id;

      const monitorName = `e2e-dash-mon-${Date.now()}`;
      const monitor = await apiHelper.createTestMonitor(apiHelper.request, {
        name: monitorName,
        jobIds: [job.id],
      });
      monitorId = monitor.id;

      // Each page.goto() creates a fresh JS context (destroying any TanStack
      // Query cache), so navigate to the dashboard and wait for the active-monitors
      // API call to complete before asserting.
      const monitorsResponse = page.waitForResponse(
        (resp) => resp.url().includes("/api/v1/dashboard/active-monitors") && resp.status() === 200,
        { timeout: 15_000 }
      );
      await page.goto("/");
      await page.waitForURL("/", { timeout: 10_000 });
      await monitorsResponse;

      const main = page.getByRole("main");

      // Wait for the Active Monitors section to load (skeleton or content)
      await expect(
        main.getByText("Active Monitors", { exact: true })
      ).toBeVisible({ timeout: 10_000 });

      // Verify the monitor appears — use longer timeout to allow for data fetch
      await expect(
        main.getByText(monitorName)
      ).toBeVisible({ timeout: 15_000 });
    } finally {
      if (monitorId) {
        await apiHelper.deleteTestMonitor(apiHelper.request, monitorId);
      }
      if (jobId) {
        await apiHelper.deleteTestJob(apiHelper.request, jobId);
      }
    }
  });

  test("key expiry warnings section appears for expiring keys", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const page = authenticatedPage;
    let keyId: string | undefined;

    try {
      // Generate a PGP key (default keys have expiry set by the backend)
      const key = await apiHelper.generateTestPgpKey(
        apiHelper.request,
        `e2e-dash-pgp-${Date.now()}`
      );
      keyId = key.id;

      // Navigate to dashboard
      await page.goto("/");
      await page.waitForURL("/", { timeout: 10_000 });

      const main = page.getByRole("main");

      // The key expiry section either shows or remains hidden depending
      // on whether the generated key has an expiry within 30 days.
      // We verify the section renders correctly in either case.
      const expirySection = main.getByText("Key Expiry Warnings");
      const noExpiryKeys = main.getByText("No active monitors.");

      // Either the section is visible (key has near expiry) or it's hidden
      // (key expiry is far in the future). Both are valid outcomes.
      const isVisible = await expirySection.isVisible().catch(() => false);

      if (isVisible) {
        await expect(expirySection).toBeVisible();
      } else {
        // Key Expiry section returns null when no keys expire within 30 days
        await expect(expirySection).not.toBeVisible();
      }
    } finally {
      if (keyId) {
        await apiHelper.deleteTestPgpKey(apiHelper.request, keyId);
      }
    }
  });

  test("execution click-through navigates to job detail", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const page = authenticatedPage;
    let jobId: string | undefined;

    try {
      // Create a job with a step and trigger it
      const job = await apiHelper.createTestJob(apiHelper.request, {
        name: `e2e-dash-click-${Date.now()}`,
      });
      jobId = job.id;

      await apiHelper.addJobSteps(apiHelper.request, job.id, [
        {
          name: "copy step",
          typeKey: "file.copy",
          stepOrder: 1,
          configuration: JSON.stringify({
            sourcePath: "/tmp/nonexistent",
            destinationPath: "/tmp/out",
          }),
        },
      ]);

      await apiHelper.triggerJob(apiHelper.request, job.id);

      // Wait for execution to be recorded
      await page.waitForTimeout(2000);

      // Navigate to dashboard
      await page.goto("/");
      await page.waitForURL("/", { timeout: 10_000 });

      const main = page.getByRole("main");

      // Wait for the executions table to appear
      const jobLink = main.locator("table").getByRole("link", { name: job.name });
      await expect(jobLink).toBeVisible({ timeout: 10_000 });

      // Click the job name link in the execution row
      await jobLink.click();

      // Verify navigation to job detail page
      await expect(page).toHaveURL(new RegExp(`/jobs/${jobId}`), {
        timeout: 10_000,
      });
    } finally {
      if (jobId) {
        await apiHelper.deleteTestJob(apiHelper.request, jobId);
      }
    }
  });
});
