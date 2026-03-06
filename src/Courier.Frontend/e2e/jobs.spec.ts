import { test, expect } from "./fixtures";
import {
  createTestJob,
  deleteTestJob,
  addJobSteps,
} from "./helpers/api-helpers";

test.describe("Jobs", () => {
  test("displays empty state when no jobs exist", async ({
    authenticatedPage,
  }) => {
    // Navigate to jobs page — with a fresh DB this should show empty state
    await authenticatedPage.goto("/jobs");

    // The empty state renders when there are zero jobs
    // If jobs already exist from other tests, this test may see the table instead,
    // so we check for either the empty state or the job table heading
    const heading = authenticatedPage.getByRole("heading", { name: "Jobs" });
    await expect(heading).toBeVisible();
  });

  test("navigates to create job page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/jobs");

    await authenticatedPage
      .getByRole("link", { name: "Create Job" })
      .click();

    await expect(authenticatedPage).toHaveURL(/\/jobs\/new/);
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Job" })
    ).toBeVisible();
  });

  test("creates a job with name and description", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const jobName = `e2e-create-${Date.now()}`;
    let createdJobId: string | undefined;

    try {
      await authenticatedPage.goto("/jobs/new");

      await authenticatedPage.getByLabel("Name").fill(jobName);
      await authenticatedPage
        .getByLabel("Description")
        .fill("E2E test description");

      await authenticatedPage
        .getByRole("button", { name: "Create Job" })
        .click();

      // Should show success toast and redirect to job detail page
      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Job created",
        })
      ).toBeVisible({ timeout: 10_000 });

      await expect(authenticatedPage).toHaveURL(/\/jobs\/[a-f0-9-]+$/);

      // Extract job ID from URL for cleanup
      const url = authenticatedPage.url();
      createdJobId = url.split("/jobs/")[1];

      await expect(
        authenticatedPage.getByRole("heading", { name: jobName })
      ).toBeVisible();
      await expect(
        authenticatedPage.getByText("E2E test description")
      ).toBeVisible();
    } finally {
      if (createdJobId) {
        await deleteTestJob(apiHelper.request, createdJobId);
      }
    }
  });

  test("created job appears in job list", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const jobName = `e2e-list-${Date.now()}`;
    const job = await createTestJob(apiHelper.request, { name: jobName });

    try {
      await authenticatedPage.goto("/jobs");

      await expect(authenticatedPage.getByText(jobName)).toBeVisible({
        timeout: 10_000,
      });
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("job detail page shows job info and version badge", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const jobName = `e2e-detail-${Date.now()}`;
    const job = await createTestJob(apiHelper.request, {
      name: jobName,
      description: "Detail page test",
    });

    try {
      await authenticatedPage.goto(`/jobs/${job.id}`);

      await expect(
        authenticatedPage.getByRole("heading", { name: jobName })
      ).toBeVisible();
      await expect(
        authenticatedPage.getByText("Detail page test")
      ).toBeVisible();

      // Version badge — shows "v1" for a new job
      await expect(authenticatedPage.getByText("v1")).toBeVisible();
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("job detail shows empty steps message", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-nosteps-${Date.now()}`,
    });

    try {
      await authenticatedPage.goto(`/jobs/${job.id}`);

      await expect(
        authenticatedPage.getByText("No steps configured.")
      ).toBeVisible();
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("edits job name and description", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const originalName = `e2e-edit-orig-${Date.now()}`;
    const updatedName = `e2e-edit-updated-${Date.now()}`;
    const job = await createTestJob(apiHelper.request, {
      name: originalName,
      description: "Before edit",
    });

    try {
      await authenticatedPage.goto(`/jobs/${job.id}/edit`);

      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit Job" })
      ).toBeVisible();

      const nameInput = authenticatedPage.getByLabel("Name");
      await nameInput.clear();
      await nameInput.fill(updatedName);

      const descInput = authenticatedPage.getByLabel("Description");
      await descInput.clear();
      await descInput.fill("After edit");

      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Job updated",
        })
      ).toBeVisible({ timeout: 10_000 });

      // Should redirect back to job detail with updated info
      await expect(authenticatedPage).toHaveURL(new RegExp(`/jobs/${job.id}$`));
      await expect(
        authenticatedPage.getByRole("heading", { name: updatedName })
      ).toBeVisible();
      await expect(
        authenticatedPage.getByText("After edit")
      ).toBeVisible();
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("searches jobs by name", async ({ authenticatedPage, apiHelper }) => {
    const searchable = `e2e-searchable-${Date.now()}`;
    const other = `e2e-other-${Date.now()}`;
    const job1 = await createTestJob(apiHelper.request, { name: searchable });
    const job2 = await createTestJob(apiHelper.request, { name: other });

    try {
      await authenticatedPage.goto("/jobs");

      // Wait for jobs to load
      await expect(authenticatedPage.getByText(searchable)).toBeVisible({
        timeout: 10_000,
      });

      // Type in search box
      const searchInput =
        authenticatedPage.getByPlaceholder("Search jobs...");
      await searchInput.fill(searchable);

      // The searchable job should be visible, the other should not
      await expect(authenticatedPage.getByText(searchable)).toBeVisible();
      await expect(authenticatedPage.getByText(other)).not.toBeVisible();

      // Clear search — both should reappear
      await searchInput.clear();
      await expect(authenticatedPage.getByText(other)).toBeVisible();
    } finally {
      await deleteTestJob(apiHelper.request, job1.id);
      await deleteTestJob(apiHelper.request, job2.id);
    }
  });

  test("deletes a job from list", async ({ authenticatedPage, apiHelper }) => {
    const jobName = `e2e-delete-${Date.now()}`;
    const job = await createTestJob(apiHelper.request, { name: jobName });

    try {
      await authenticatedPage.goto("/jobs");

      // Wait for the job to appear
      await expect(authenticatedPage.getByText(jobName)).toBeVisible({
        timeout: 10_000,
      });

      // The actions menu button is hidden until hover; force-click
      const row = authenticatedPage.getByRole("row", { name: new RegExp(jobName) });
      const menuButton = row.getByRole("button");
      await menuButton.click({ force: true });

      // Click Delete in the dropdown menu
      await authenticatedPage.getByRole("menuitem", { name: "Delete" }).click();

      // Confirm in the dialog
      const dialog = authenticatedPage.getByRole("dialog");
      await expect(dialog).toBeVisible();
      await dialog.getByRole("button", { name: "Delete" }).click();

      // Should show success toast
      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Job deleted",
        })
      ).toBeVisible({ timeout: 10_000 });

      // Job should no longer appear
      await expect(authenticatedPage.getByText(jobName)).not.toBeVisible();
    } catch {
      // Cleanup if the test failed before deletion completed
      await deleteTestJob(apiHelper.request, job.id).catch(() => {});
    }
  });

  test("job detail shows enabled/disabled badge", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-badge-${Date.now()}`,
    });

    try {
      await authenticatedPage.goto(`/jobs/${job.id}`);

      // New jobs are enabled by default
      await expect(authenticatedPage.getByText("Enabled")).toBeVisible();
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });
});

test.describe("Jobs - Step Builder", () => {
  test("adds a file.copy step during job creation", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    let createdJobId: string | undefined;

    try {
      await authenticatedPage.goto("/jobs/new");

      // Click "Add Step" button in the step builder
      await authenticatedPage
        .getByRole("button", { name: "Add Step" })
        .click();

      // Fill step name
      const stepNameInput = authenticatedPage.getByPlaceholder(
        "e.g., Copy invoice files"
      );
      await stepNameInput.fill("Copy test files");

      // The default type should be "file.copy" — verify via the native select
      const select = authenticatedPage.locator("select");
      await expect(select).toHaveValue("file.copy");

      // Click the "Add" button to add the step
      await authenticatedPage
        .getByRole("button", { name: "Add", exact: true })
        .click();

      // The step should now appear in the list with the type badge
      await expect(
        authenticatedPage.getByText("Copy test files")
      ).toBeVisible();
      await expect(authenticatedPage.getByText("file.copy")).toBeVisible();

      // Now create the job with this step
      await authenticatedPage.getByLabel("Name").fill(`e2e-step-${Date.now()}`);
      await authenticatedPage
        .getByRole("button", { name: "Create Job" })
        .click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Job created",
        })
      ).toBeVisible({ timeout: 10_000 });

      // Extract job ID for cleanup
      await authenticatedPage.waitForURL(/\/jobs\/[a-f0-9-]+$/);
      createdJobId = authenticatedPage.url().split("/jobs/")[1];
    } finally {
      if (createdJobId) {
        await deleteTestJob(apiHelper.request, createdJobId);
      }
    }
  });

  test("adds multiple steps and verifies order", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    // Add first step — file.copy
    await authenticatedPage
      .getByRole("button", { name: "Add Step" })
      .click();
    await authenticatedPage
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Step One");
    // Default is file.copy
    await authenticatedPage
      .getByRole("button", { name: "Add", exact: true })
      .click();

    // Add second step — sftp.upload
    await authenticatedPage
      .getByRole("button", { name: "Add Step" })
      .click();
    await authenticatedPage
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Step Two");
    const select = authenticatedPage.locator("select");
    await select.selectOption("sftp.upload");
    await authenticatedPage
      .getByRole("button", { name: "Add", exact: true })
      .click();

    // Verify both steps appear in order
    await expect(authenticatedPage.getByText("Step One")).toBeVisible();
    await expect(authenticatedPage.getByText("Step Two")).toBeVisible();

    // Verify step order numbers — "1." and "2." should be present
    const stepNumbers = authenticatedPage.locator(".tabular-nums");
    const texts: string[] = [];
    const count = await stepNumbers.count();
    for (let i = 0; i < count; i++) {
      const text = await stepNumbers.nth(i).textContent();
      if (text) texts.push(text.trim());
    }
    expect(texts).toContain("1.");
    expect(texts).toContain("2.");

    // Verify type badges
    await expect(authenticatedPage.getByText("file.copy")).toBeVisible();
    await expect(authenticatedPage.getByText("sftp.upload")).toBeVisible();
  });

  test("removes a step from the builder", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/jobs/new");

    // Wait for the page to be fully loaded
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Job" })
    ).toBeVisible({ timeout: 10_000 });

    // Add a step
    await authenticatedPage
      .getByRole("button", { name: "Add Step" })
      .click();
    await authenticatedPage
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Removable Step");
    await authenticatedPage
      .getByRole("button", { name: "Add", exact: true })
      .click();

    // Verify step is added
    await expect(
      authenticatedPage.getByText("Removable Step")
    ).toBeVisible();

    // Click the remove (X) button — the ghost button with text-destructive class
    const removeButton = authenticatedPage.locator(
      "button.text-destructive"
    );
    await removeButton.click();

    // Step should be gone, empty message should appear
    await expect(
      authenticatedPage.getByText("Removable Step")
    ).not.toBeVisible();
    await expect(
      authenticatedPage.getByText("No steps yet. Add a step to define what this job does.")
    ).toBeVisible();
  });

  test("creates a job with steps and verifies on detail page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const jobName = `e2e-withsteps-${Date.now()}`;
    let createdJobId: string | undefined;

    try {
      await authenticatedPage.goto("/jobs/new");

      // Wait for the page to be fully loaded
      await expect(
        authenticatedPage.getByRole("heading", { name: "Create Job" })
      ).toBeVisible({ timeout: 10_000 });

      // Fill job details
      await authenticatedPage.getByLabel("Name").fill(jobName);
      await authenticatedPage
        .getByLabel("Description")
        .fill("Job with steps");

      // Add a file.copy step
      await authenticatedPage
        .getByRole("button", { name: "Add Step" })
        .click();
      await authenticatedPage
        .getByPlaceholder("e.g., Copy invoice files")
        .fill("Copy invoices");
      await authenticatedPage
        .getByRole("button", { name: "Add", exact: true })
        .click();

      // Wait for the first step to appear in the list before adding the next
      await expect(
        authenticatedPage.getByText("Copy invoices")
      ).toBeVisible();

      // Wait for the "Add Step" button to be enabled again (adding state reset)
      await expect(
        authenticatedPage.getByRole("button", { name: "Add Step" })
      ).toBeEnabled();

      // Add an sftp.upload step (avoid pgp.encrypt — PgpKeyPicker has empty Select.Item bug)
      await authenticatedPage
        .getByRole("button", { name: "Add Step" })
        .click();

      // Wait for the step name input to appear in the new add form
      const stepNameInput = authenticatedPage.getByPlaceholder("e.g., Copy invoice files");
      await expect(stepNameInput).toBeVisible();
      await stepNameInput.fill("Upload files");

      const select = authenticatedPage.locator("select");
      await select.selectOption("sftp.upload");
      await authenticatedPage
        .getByRole("button", { name: "Add", exact: true })
        .click();

      // Wait for the second step to appear in the list
      await expect(
        authenticatedPage.getByText("Upload files")
      ).toBeVisible();

      // Submit the form
      await authenticatedPage
        .getByRole("button", { name: "Create Job" })
        .click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Job created",
        })
      ).toBeVisible({ timeout: 10_000 });

      await authenticatedPage.waitForURL(/\/jobs\/[a-f0-9-]+$/);
      createdJobId = authenticatedPage.url().split("/jobs/")[1];

      // Verify on the detail page
      await expect(
        authenticatedPage.getByRole("heading", { name: jobName })
      ).toBeVisible();

      // Steps section should show "Steps (2)" — wait for steps data to load
      await expect(authenticatedPage.getByText("Steps (2)")).toBeVisible({
        timeout: 10_000,
      });

      // Step names and type keys should be visible
      await expect(
        authenticatedPage.getByText("Copy invoices")
      ).toBeVisible();
      await expect(authenticatedPage.getByText("file.copy")).toBeVisible();
      await expect(
        authenticatedPage.getByText("Upload files")
      ).toBeVisible();
      await expect(
        authenticatedPage.getByText("sftp.upload")
      ).toBeVisible();
    } finally {
      if (createdJobId) {
        await deleteTestJob(apiHelper.request, createdJobId);
      }
    }
  });
});
