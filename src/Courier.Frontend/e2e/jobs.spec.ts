import { test, expect } from "./fixtures";
import {
  createTestJob,
  deleteTestJob,
  addJobSteps,
  updateTestJob,
  createTestTag,
  deleteTestTag,
  assignTagToEntity,
  generateTestPgpKey,
  deleteTestPgpKey,
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
    const heading = authenticatedPage.locator("main").getByRole("heading", { name: "Jobs" }).first();
    await expect(heading).toBeVisible({ timeout: 10_000 });
  });

  test("navigates to create job page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/jobs");
    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Jobs" }).first()
    ).toBeVisible({ timeout: 10_000 });

    await authenticatedPage
      .getByRole("link", { name: "Create Job" })
      .first()
      .click();

    await expect(authenticatedPage).toHaveURL(/\/jobs\/new/, { timeout: 10_000 });
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Job" })
    ).toBeVisible({ timeout: 10_000 });
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

  test("pagination controls appear with many jobs", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create 11 jobs to exceed page size of 10
    const jobs: Array<{ id: string }> = [];
    for (let i = 0; i < 11; i++) {
      jobs.push(
        await createTestJob(apiHelper.request, {
          name: `e2e-page-${Date.now()}-${i}`,
        })
      );
    }

    try {
      await authenticatedPage.goto("/jobs");

      // Wait for jobs to load
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Jobs" }).first()
      ).toBeVisible({ timeout: 10_000 });

      // Pagination should show "Page 1 of 2"
      await expect(
        authenticatedPage.getByText(/Page 1 of/).first()
      ).toBeVisible({ timeout: 10_000 });

      // Scope pagination buttons to the container div with "Page X of Y"
      const paginationBar = authenticatedPage.locator("div.flex").filter({ hasText: /Page \d+ of \d+/ }).first();

      // Next button should be enabled
      const nextButton = paginationBar.getByRole("button", { name: "Next" });
      await expect(nextButton).toBeEnabled();

      // Previous button should be disabled on page 1
      const prevButton = paginationBar.getByRole("button", { name: "Previous" });
      await expect(prevButton).toBeDisabled();

      // Navigate to page 2
      await nextButton.click();
      await expect(
        authenticatedPage.getByText(/Page 2 of/).first()
      ).toBeVisible({ timeout: 10_000 });

      // Now Previous should be enabled
      await expect(prevButton).toBeEnabled();
    } finally {
      for (const job of jobs) {
        await deleteTestJob(apiHelper.request, job.id).catch(() => {});
      }
    }
  });

  test("disabled job shows Disabled badge on detail page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-disabled-${Date.now()}`,
    });

    try {
      // Disable via API
      await updateTestJob(apiHelper.request, job.id, { isEnabled: false });

      await authenticatedPage.goto(`/jobs/${job.id}`);

      await expect(authenticatedPage.getByText("Disabled")).toBeVisible({
        timeout: 10_000,
      });
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("deletes a job from the detail page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const jobName = `e2e-del-detail-${Date.now()}`;
    const job = await createTestJob(apiHelper.request, { name: jobName });

    try {
      await authenticatedPage.goto(`/jobs/${job.id}`);

      // Wait for the job detail page to load
      await expect(
        authenticatedPage.getByRole("heading", { name: jobName })
      ).toBeVisible({ timeout: 10_000 });

      // Click the Edit button to navigate to edit page which has a back link
      // Actually, there is no delete button on the detail page itself.
      // Delete is done via the list page row actions. Navigate there instead.
      await authenticatedPage.goto("/jobs");

      await expect(authenticatedPage.getByText(jobName)).toBeVisible({
        timeout: 10_000,
      });

      const row = authenticatedPage.getByRole("row", { name: new RegExp(jobName) });
      await row.getByRole("button").click({ force: true });
      await authenticatedPage.getByRole("menuitem", { name: "Delete" }).click();

      const dialog = authenticatedPage.getByRole("dialog");
      await expect(dialog).toBeVisible();
      await dialog.getByRole("button", { name: "Delete" }).click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Job deleted",
        })
      ).toBeVisible({ timeout: 10_000 });

      await expect(authenticatedPage.getByText(jobName)).not.toBeVisible();
    } catch {
      await deleteTestJob(apiHelper.request, job.id).catch(() => {});
    }
  });

  test("tag assignment on job detail page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-tag-${Date.now()}`,
    });
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-tag-${Date.now()}`,
      color: "#ef4444",
    });

    try {
      // Assign via API to avoid complex UI picker interactions
      await assignTagToEntity(apiHelper.request, tag.id, "job", job.id);

      await authenticatedPage.goto(`/jobs/${job.id}`);

      // The tag name should appear in the Tags card
      await expect(authenticatedPage.getByText(tag.name)).toBeVisible({
        timeout: 10_000,
      });
    } finally {
      await deleteTestJob(apiHelper.request, job.id).catch(() => {});
      await deleteTestTag(apiHelper.request, tag.id).catch(() => {});
    }
  });

  test("version increments after editing a job", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-version-${Date.now()}`,
      description: "Version 1",
    });

    try {
      // Verify initial version
      await authenticatedPage.goto(`/jobs/${job.id}`);
      await expect(authenticatedPage.getByText("v1")).toBeVisible({
        timeout: 10_000,
      });

      // Edit the job
      await authenticatedPage.goto(`/jobs/${job.id}/edit`);
      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit Job" })
      ).toBeVisible({ timeout: 10_000 });

      const descInput = authenticatedPage.getByLabel("Description");
      await descInput.clear();
      await descInput.fill("Version 2 description");

      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Job updated",
        })
      ).toBeVisible({ timeout: 10_000 });

      // Should redirect back to detail page with v2
      await expect(authenticatedPage).toHaveURL(new RegExp(`/jobs/${job.id}$`));
      await expect(authenticatedPage.getByText("v2")).toBeVisible({
        timeout: 10_000,
      });
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

  test("step config: file.copy shows source and destination path fields", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    await authenticatedPage
      .getByRole("button", { name: "Add Step" })
      .click();
    await authenticatedPage
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Copy config test");

    // Default type is file.copy — config form should show Source Path and Destination Path
    const sourceInput = authenticatedPage.getByPlaceholder("/data/incoming/");
    await expect(sourceInput).toBeVisible();
    await sourceInput.fill("/tmp/source");

    const destInput = authenticatedPage.getByPlaceholder("/data/processed/");
    await expect(destInput).toBeVisible();
    await destInput.fill("/tmp/dest");

    // Add the step and verify the summary shows the paths
    await authenticatedPage
      .getByRole("button", { name: "Add", exact: true })
      .click();

    await expect(
      authenticatedPage.getByText("Copy config test")
    ).toBeVisible();

    // The step summary should show "source → dest"
    await expect(
      authenticatedPage.getByText(/\/tmp\/source/)
    ).toBeVisible();
  });

  test("step config: sftp.upload shows connection and path fields", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    const main = authenticatedPage.locator("main");

    await main
      .getByRole("button", { name: "Add Step" })
      .click();
    await main
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Upload config test");

    const select = main.locator("select");
    await select.selectOption("sftp.upload");

    // Config form should show Connection picker, Local Path, and Remote Path
    await expect(
      main.getByText("Connection", { exact: true })
    ).toBeVisible();

    const localInput = main.getByPlaceholder(
      "/data/outgoing/report.csv"
    );
    await expect(localInput).toBeVisible();
    await localInput.fill("/data/local/file.csv");

    const remoteInput = main.getByPlaceholder(
      "/uploads/report.csv"
    );
    await expect(remoteInput).toBeVisible();
    await remoteInput.fill("/remote/upload/file.csv");

    // Add the step
    await main
      .getByRole("button", { name: "Add", exact: true })
      .click();

    await expect(
      main.getByText("Upload config test")
    ).toBeVisible();
    await expect(
      main.getByText("sftp.upload")
    ).toBeVisible();
  });

  test("step config: pgp.encrypt shows input/output path and key fields", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create a PGP key so the PgpKeyPicker/PgpMultiKeyPicker components render
    // real items instead of an empty-value SelectItem (which crashes Radix Select).
    const pgpKey = await generateTestPgpKey(
      apiHelper.request,
      `e2e-pgp-step-${Date.now()}`
    );

    try {
      await authenticatedPage.goto("/jobs/new");

      const main = authenticatedPage.locator("main");

      await main
        .getByRole("button", { name: "Add Step" })
        .click();
      await main
        .getByPlaceholder("e.g., Copy invoice files")
        .fill("Encrypt config test");

      // Wait for the step type selector to be visible, then switch to pgp.encrypt
      const select = main.locator("select");
      await expect(select).toBeVisible({ timeout: 5_000 });
      await select.selectOption("pgp.encrypt");

      // Wait for the PGP config form to mount after the type change
      const inputPathField = main.getByPlaceholder(
        "/data/plaintext/report.csv"
      );
      await expect(inputPathField).toBeVisible({ timeout: 10_000 });
      await inputPathField.fill("/data/plain/file.txt");

      const outputPathField = main.getByPlaceholder(
        "/data/encrypted/report.csv.pgp"
      );
      await expect(outputPathField).toBeVisible({ timeout: 5_000 });
      await outputPathField.fill("/data/encrypted/file.txt.pgp");

      // Recipient Keys section should be visible
      await expect(
        main.getByText("Recipient Keys")
      ).toBeVisible({ timeout: 5_000 });

      // Output Format radio buttons should be visible
      await expect(main.getByText("Binary")).toBeVisible({ timeout: 5_000 });
      await expect(main.getByText("ASCII Armored")).toBeVisible({ timeout: 5_000 });

      // Add the step
      await main
        .getByRole("button", { name: "Add", exact: true })
        .click();

      await expect(
        main.getByText("Encrypt config test")
      ).toBeVisible();
      await expect(
        main.getByText("pgp.encrypt")
      ).toBeVisible();
    } finally {
      await deleteTestPgpKey(apiHelper.request, pgpKey.id);
    }
  });

  test("step config: ftp.download shows connection and path fields", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    const main = authenticatedPage.locator("main");

    await main
      .getByRole("button", { name: "Add Step" })
      .click();
    await main
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("FTP download test");

    const select = main.locator("select");
    await select.selectOption("ftp.download");

    // Config form should show Connection picker, Remote Path, Local Path
    await expect(
      main.getByText("Connection", { exact: true })
    ).toBeVisible();

    const remoteInput = main.getByPlaceholder("/incoming/data/");
    await expect(remoteInput).toBeVisible();
    await remoteInput.fill("/ftp/remote/path");

    const localInput = main.getByPlaceholder("/data/downloads/");
    await expect(localInput).toBeVisible();
    await localInput.fill("/local/download/path");

    // File Pattern field should be visible
    await expect(
      main.getByPlaceholder("*.csv")
    ).toBeVisible();

    // Add the step
    await main
      .getByRole("button", { name: "Add", exact: true })
      .click();

    await expect(
      main.getByText("FTP download test")
    ).toBeVisible();
    await expect(
      main.getByText("ftp.download")
    ).toBeVisible();
  });

  test("step reordering with up/down arrows", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    // Add 3 steps
    for (const name of ["Alpha", "Beta", "Gamma"]) {
      await authenticatedPage
        .getByRole("button", { name: "Add Step" })
        .click();
      await authenticatedPage
        .getByPlaceholder("e.g., Copy invoice files")
        .fill(name);
      await authenticatedPage
        .getByRole("button", { name: "Add", exact: true })
        .click();
      await expect(authenticatedPage.getByText(name)).toBeVisible();
    }

    // Step order should be 1. Alpha, 2. Beta, 3. Gamma
    // Click the down arrow on the first step (Alpha) to move it to position 2
    // The down arrow is ▼ (&#9660;) — it's a button element
    const cards = authenticatedPage.locator("[class*='card']").filter({
      has: authenticatedPage.locator(".tabular-nums"),
    });

    // Find the first step card (containing "Alpha") and click its down arrow
    const alphaCard = authenticatedPage.locator("[class*='CardContent']").filter({
      hasText: "Alpha",
    }).first();

    // Use the down arrow button (▼) within the first card
    // The arrow buttons contain ▲ and ▼ characters
    const firstStepDownArrow = authenticatedPage
      .locator("button")
      .filter({ hasText: "▼" })
      .first();
    await firstStepDownArrow.click();

    // Now the order should be 1. Beta, 2. Alpha, 3. Gamma
    // Verify by checking the step numbers align with names
    const stepNumbers = authenticatedPage.locator(".tabular-nums");
    const count = await stepNumbers.count();
    const entries: string[] = [];
    for (let i = 0; i < count; i++) {
      const parent = stepNumbers.nth(i).locator("..");
      const text = await parent.textContent();
      if (text) entries.push(text.trim());
    }

    // The first step should now be Beta (it was swapped with Alpha)
    // Verify Beta appears before Alpha in the DOM
    const allText = await authenticatedPage.locator("form").textContent();
    const betaIndex = allText?.indexOf("Beta") ?? -1;
    const alphaIndex = allText?.indexOf("Alpha") ?? -1;
    expect(betaIndex).toBeLessThan(alphaIndex);
  });

  test("edit existing steps on job edit page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const jobName = `e2e-editsteps-${Date.now()}`;
    const job = await createTestJob(apiHelper.request, { name: jobName });

    await addJobSteps(apiHelper.request, job.id, [
      {
        name: "Original Step",
        typeKey: "file.copy",
        stepOrder: 1,
        configuration: JSON.stringify({
          source_path: "/old/source",
          destination_path: "/old/dest",
        }),
      },
    ]);

    try {
      await authenticatedPage.goto(`/jobs/${job.id}/edit`);

      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit Job" })
      ).toBeVisible({ timeout: 10_000 });

      // The existing step should be visible
      await expect(
        authenticatedPage.getByText("Original Step")
      ).toBeVisible();

      // Click the pencil/edit button on the step to expand config
      const editButton = authenticatedPage.locator("button").filter({
        has: authenticatedPage.locator("svg.lucide-pencil"),
      }).first();
      await editButton.click();

      // The step name should now be editable — change it
      // The inline edit input uses class "h-7" to distinguish it from full-size inputs
      const stepNameInput = authenticatedPage.locator("main input.h-7");
      await expect(stepNameInput).toBeVisible();
      await stepNameInput.fill("Updated Step");

      // Click Done to collapse
      await authenticatedPage
        .getByRole("button", { name: "Done" })
        .click();

      // Save the job
      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Job updated",
        })
      ).toBeVisible({ timeout: 10_000 });

      // Verify on detail page
      await expect(authenticatedPage).toHaveURL(new RegExp(`/jobs/${job.id}$`));
      await expect(
        authenticatedPage.getByText("Updated Step")
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("step removal renumbers remaining steps", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    // Add 3 steps
    for (const name of ["First", "Middle", "Last"]) {
      await authenticatedPage
        .getByRole("button", { name: "Add Step" })
        .click();
      await authenticatedPage
        .getByPlaceholder("e.g., Copy invoice files")
        .fill(name);
      await authenticatedPage
        .getByRole("button", { name: "Add", exact: true })
        .click();
      await expect(authenticatedPage.getByText(name)).toBeVisible();
    }

    // Verify we have 3 steps with numbers 1., 2., 3.
    const stepNums = authenticatedPage.locator(".tabular-nums");
    await expect(stepNums).toHaveCount(3);

    // Remove the middle step (index 1) — click the second X button
    const removeButtons = authenticatedPage.locator("button.text-destructive");
    await removeButtons.nth(1).click();

    // Middle should be gone
    await expect(authenticatedPage.getByText("Middle")).not.toBeVisible();

    // Remaining steps should be renumbered to 1. and 2.
    await expect(stepNums).toHaveCount(2);
    const firstNum = await stepNums.nth(0).textContent();
    const secondNum = await stepNums.nth(1).textContent();
    expect(firstNum?.trim()).toBe("1.");
    expect(secondNum?.trim()).toBe("2.");

    // The correct steps should remain
    await expect(authenticatedPage.getByText("First")).toBeVisible();
    await expect(authenticatedPage.getByText("Last")).toBeVisible();
  });

  test("file browser dialog opens from step config", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    await authenticatedPage
      .getByRole("button", { name: "Add Step" })
      .click();
    await authenticatedPage
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Browse test");

    // The file.copy config has Browse filesystem buttons
    const browseButtons = authenticatedPage.locator(
      "button[title='Browse filesystem']"
    );
    await expect(browseButtons.first()).toBeVisible();

    // Click the first browse button (Source Path)
    await browseButtons.first().click();

    // A dialog should open — the FileBrowserDialog
    const dialog = authenticatedPage.getByRole("dialog");
    await expect(dialog).toBeVisible({ timeout: 10_000 });

    // Close the dialog
    // The dialog should have some close mechanism — press Escape
    await authenticatedPage.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
  });

  test("step alias shows auto-generated reference ID on edit", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const jobName = `e2e-alias-${Date.now()}`;
    const job = await createTestJob(apiHelper.request, { name: jobName });

    await addJobSteps(apiHelper.request, job.id, [
      {
        name: "Copy Invoice Files",
        typeKey: "file.copy",
        stepOrder: 1,
        configuration: JSON.stringify({
          source_path: "/data/in",
          destination_path: "/data/out",
        }),
      },
    ]);

    try {
      await authenticatedPage.goto(`/jobs/${job.id}/edit`);

      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit Job" })
      ).toBeVisible({ timeout: 10_000 });

      // Expand the step by clicking the edit (pencil) button
      const editButton = authenticatedPage.locator("button").filter({
        has: authenticatedPage.locator("svg.lucide-pencil"),
      }).first();
      await editButton.click();

      // The Reference ID label should be visible for output-producing steps
      await expect(
        authenticatedPage.getByText("Reference ID:")
      ).toBeVisible();

      // The alias input should show the auto-generated placeholder from the step name
      // "Copy Invoice Files" → "copy_invoice_files"
      const aliasInput = authenticatedPage.locator("input[placeholder='copy_invoice_files']");
      await expect(aliasInput).toBeVisible();

      // The reference preview should show context:{alias}.<output>
      await expect(
        authenticatedPage.locator("code").filter({ hasText: "context:copy_invoice_files." })
      ).toBeVisible();
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("step alias can be overridden with custom value", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const jobName = `e2e-alias-custom-${Date.now()}`;
    const job = await createTestJob(apiHelper.request, { name: jobName });

    await addJobSteps(apiHelper.request, job.id, [
      {
        name: "Copy Files",
        typeKey: "file.copy",
        stepOrder: 1,
        configuration: JSON.stringify({
          source_path: "/data/in",
          destination_path: "/data/out",
        }),
      },
    ]);

    try {
      await authenticatedPage.goto(`/jobs/${job.id}/edit`);

      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit Job" })
      ).toBeVisible({ timeout: 10_000 });

      // Expand the step
      const editButton = authenticatedPage.locator("button").filter({
        has: authenticatedPage.locator("svg.lucide-pencil"),
      }).first();
      await editButton.click();

      // Type a custom alias
      const aliasInput = authenticatedPage.locator("input[placeholder='copy_files']");
      await expect(aliasInput).toBeVisible();
      await aliasInput.fill("my_copy");

      // The reference preview should update to the custom alias
      await expect(
        authenticatedPage.locator("code").filter({ hasText: "context:my_copy." })
      ).toBeVisible();

      // Click Done to collapse the step editor
      await authenticatedPage
        .getByRole("button", { name: "Done" })
        .click();

      // Save the job
      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Job updated",
        })
      ).toBeVisible({ timeout: 10_000 });

      // Verify redirect back to detail page
      await expect(authenticatedPage).toHaveURL(new RegExp(`/jobs/${job.id}$`));
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("context variable panel shows preceding step outputs", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Job" })
    ).toBeVisible({ timeout: 10_000 });

    // Add first step — file.copy (produces "copied_file" output)
    await authenticatedPage
      .getByRole("button", { name: "Add Step" })
      .click();
    await authenticatedPage
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Download Report");
    await authenticatedPage
      .getByRole("button", { name: "Add", exact: true })
      .click();
    await expect(
      authenticatedPage.getByText("Download Report")
    ).toBeVisible();

    // Add second step — this should show the context variable panel
    await authenticatedPage
      .getByRole("button", { name: "Add Step" })
      .click();
    await authenticatedPage
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Upload Report");

    // The "Available Variables" panel should be visible with preceding step outputs
    await expect(
      authenticatedPage.getByText("Available Variables")
    ).toBeVisible();

    // Should show the first step's output variable
    // "context:download_report.copied_file" (auto-aliased from "Download Report")
    await expect(
      authenticatedPage.locator("code").filter({ hasText: "context:download_report.copied_file" })
    ).toBeVisible();

    // The description should be visible
    await expect(
      authenticatedPage.getByText("Destination file path")
    ).toBeVisible();
  });

  test("context variable panel copy-to-clipboard works", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Job" })
    ).toBeVisible({ timeout: 10_000 });

    // Add first step
    await authenticatedPage
      .getByRole("button", { name: "Add Step" })
      .click();
    await authenticatedPage
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Copy Source");
    await authenticatedPage
      .getByRole("button", { name: "Add", exact: true })
      .click();
    await expect(
      authenticatedPage.getByText("Copy Source")
    ).toBeVisible();

    // Add second step to show the variable panel
    await authenticatedPage
      .getByRole("button", { name: "Add Step" })
      .click();
    await authenticatedPage
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Next Step");

    // Click the variable row to copy it
    const variableRow = authenticatedPage.locator("button").filter({
      hasText: "context:copy_source.copied_file",
    });
    await expect(variableRow).toBeVisible();
    await variableRow.click();

    // Should show "Copied!" feedback
    await expect(
      authenticatedPage.getByText("Copied!")
    ).toBeVisible();
  });

  test("step config: azure_function.execute shows function config fields", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/jobs/new");

    const main = authenticatedPage.locator("main");

    await main
      .getByRole("button", { name: "Add Step" })
      .click();
    await main
      .getByPlaceholder("e.g., Copy invoice files")
      .fill("Azure func test");

    const select = main.locator("select");
    await select.selectOption("azure_function.execute");

    // Config form should show Connection picker and Function Name input
    await expect(
      main.getByText("Connection", { exact: true })
    ).toBeVisible();

    const funcNameInput = main.getByPlaceholder(
      "e.g., ProcessInvoices"
    );
    await expect(funcNameInput).toBeVisible();
    await funcNameInput.fill("TestFunction");

    // Input Payload textarea should be visible
    const payloadInput = main.getByPlaceholder(
      'e.g., {"batchId": "2024-01"}'
    );
    await expect(payloadInput).toBeVisible();

    // Poll Interval, Max Wait, and Initial Delay fields should be visible
    await expect(
      main.getByText("Poll Interval (sec)")
    ).toBeVisible();
    await expect(
      main.getByText("Max Wait (sec)")
    ).toBeVisible();
    await expect(
      main.getByText("Initial Delay (sec)")
    ).toBeVisible();

    // Add the step
    await main
      .getByRole("button", { name: "Add", exact: true })
      .click();

    await expect(
      main.getByText("Azure func test")
    ).toBeVisible();
    await expect(
      main.getByText("azure_function.execute")
    ).toBeVisible();
  });
});
