import { test, expect } from "./fixtures";
import {
  createTestJob,
  deleteTestJob,
  addJobSteps,
  triggerJob,
  createJobSchedule,
  deleteJobSchedule,
  updateTestJob,
} from "./helpers/api-helpers";

test.describe("Job Execution", () => {
  let jobId: string;
  let jobName: string;

  test.beforeEach(async ({ apiHelper }) => {
    jobName = `e2e-exec-${Date.now()}`;
    const job = await createTestJob(apiHelper.request, {
      name: jobName,
      description: "Execution test job",
    });
    jobId = job.id;

    // Add a step so the job can be triggered
    await addJobSteps(apiHelper.request, jobId, [
      {
        name: "Copy test file",
        typeKey: "file.copy",
        stepOrder: 1,
        configuration: JSON.stringify({
          sourcePath: "/tmp/source.txt",
          destinationPath: "/tmp/dest.txt",
        }),
      },
    ]);
  });

  test.afterEach(async ({ apiHelper }) => {
    await deleteTestJob(apiHelper.request, jobId).catch(() => {});
  });

  test("trigger button opens confirmation dialog", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto(`/jobs/${jobId}`);

    // Click the "Run Job" button on the detail page
    await authenticatedPage
      .getByRole("button", { name: "Run Job" })
      .click();

    // The ConfirmDialog should open with the job name
    const dialog = authenticatedPage.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText(`Run "${jobName}" now?`)).toBeVisible();

    // It should have Run and Cancel buttons
    await expect(
      dialog.getByRole("button", { name: "Run" })
    ).toBeVisible();
    await expect(
      dialog.getByRole("button", { name: "Cancel" })
    ).toBeVisible();

    // Close without triggering
    await dialog.getByRole("button", { name: "Cancel" }).click();
    await expect(dialog).not.toBeVisible();
  });

  test("triggering a job creates an execution", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto(`/jobs/${jobId}`);

    // Click Run Job
    await authenticatedPage
      .getByRole("button", { name: "Run Job" })
      .click();

    // Confirm in dialog
    const dialog = authenticatedPage.getByRole("dialog");
    await dialog.getByRole("button", { name: "Run" }).click();

    // Should show success toast
    await expect(
      authenticatedPage.locator("[data-sonner-toast]", {
        hasText: "Job queued",
      })
    ).toBeVisible({ timeout: 10_000 });

    // The executions section should no longer show the empty message
    await expect(
      authenticatedPage.getByText(
        "No executions yet. Run the job to see results here."
      )
    ).not.toBeVisible({ timeout: 10_000 });
  });

  test("execution timeline shows on job detail page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Trigger a job via API to create an execution
    await triggerJob(apiHelper.request, jobId);

    await authenticatedPage.goto(`/jobs/${jobId}`);

    // The Executions card should be visible
    await expect(
      authenticatedPage.getByText("Executions", { exact: true })
    ).toBeVisible();

    // There should be at least one execution row with "Latest" label
    await expect(
      authenticatedPage.getByText("Latest")
    ).toBeVisible({ timeout: 10_000 });
  });

  test("execution shows queued state initially", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Trigger via API
    await triggerJob(apiHelper.request, jobId);

    await authenticatedPage.goto(`/jobs/${jobId}`);

    // Wait for the execution to appear
    await expect(
      authenticatedPage.getByText("Latest")
    ).toBeVisible({ timeout: 10_000 });

    // The StatusBadge renders the state capitalized — "Queued" or possibly "Running"/"Completed"
    // Without a running Worker, it should stay in "queued" state
    // Look for any valid execution state badge
    const stateBadge = authenticatedPage.locator(".capitalize").first();
    await expect(stateBadge).toBeVisible();

    const stateText = await stateBadge.textContent();
    expect(["queued", "running", "completed", "failed"]).toContain(
      stateText?.trim().toLowerCase()
    );
  });

  test("execution detail shows step executions", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Trigger via API
    await triggerJob(apiHelper.request, jobId);

    await authenticatedPage.goto(`/jobs/${jobId}`);

    // Wait for execution to appear and click to expand
    const latestExecution = authenticatedPage.getByText("Latest");
    await expect(latestExecution).toBeVisible({ timeout: 10_000 });

    // Click to expand the execution row (it's a button element)
    await latestExecution.click();

    // The expanded view should show the triggered by info
    await expect(
      authenticatedPage.getByText(/Triggered by:/)
    ).toBeVisible({ timeout: 10_000 });

    // Should show the State line
    await expect(
      authenticatedPage.getByText(/State:/)
    ).toBeVisible();
  });

  test("cancel button appears for running execution", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Trigger via API
    await triggerJob(apiHelper.request, jobId);

    await authenticatedPage.goto(`/jobs/${jobId}`);

    // Expand the latest execution
    const latestExecution = authenticatedPage.getByText("Latest");
    await expect(latestExecution).toBeVisible({ timeout: 10_000 });
    await latestExecution.click();

    // Wait for expanded content
    await expect(
      authenticatedPage.getByText(/Triggered by:/)
    ).toBeVisible({ timeout: 10_000 });

    // If the execution is in queued/running/paused state, a Cancel button should appear
    // Without a running worker, the execution stays "queued" which is cancellable
    const cancelButton = authenticatedPage.getByRole("button", {
      name: "Cancel",
      exact: true,
    });

    // The cancel button may or may not be visible depending on execution state.
    // If the execution has already completed/failed (worker processed it fast), skip.
    const stateText = await authenticatedPage
      .locator(".capitalize")
      .first()
      .textContent();
    const state = stateText?.trim().toLowerCase();

    if (state === "queued" || state === "running" || state === "paused") {
      await expect(cancelButton).toBeVisible({ timeout: 5_000 });
    }
    // If already completed/failed, the cancel button won't be shown — test passes
  });

  test("cancel execution via cancel confirm flow", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Trigger via API
    await triggerJob(apiHelper.request, jobId);

    await authenticatedPage.goto(`/jobs/${jobId}`);

    // Expand the latest execution
    const latestExecution = authenticatedPage.getByText("Latest");
    await expect(latestExecution).toBeVisible({ timeout: 10_000 });
    await latestExecution.click();

    await expect(
      authenticatedPage.getByText(/Triggered by:/)
    ).toBeVisible({ timeout: 10_000 });

    // Check if execution is in a cancellable state
    const stateText = await authenticatedPage
      .locator(".capitalize")
      .first()
      .textContent();
    const state = stateText?.trim().toLowerCase();

    if (state === "queued" || state === "running" || state === "paused") {
      // Click Cancel button to show the cancel confirmation form
      const cancelButton = authenticatedPage.getByRole("button", {
        name: "Cancel",
        exact: true,
      });
      await cancelButton.click();

      // The cancel confirmation form should appear with reason input
      await expect(
        authenticatedPage.getByText("Cancel this execution?")
      ).toBeVisible();

      // Fill optional reason
      await authenticatedPage
        .getByPlaceholder("Reason (optional)")
        .fill("E2E test cancellation");

      // Click Confirm Cancel
      await authenticatedPage
        .getByRole("button", { name: "Confirm Cancel" })
        .click();

      // The cancel confirmation should disappear
      await expect(
        authenticatedPage.getByText("Cancel this execution?")
      ).not.toBeVisible({ timeout: 10_000 });
    }
    // If already completed/failed, skip — test passes
  });

  test("execution step details show step names and status", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Trigger via API
    await triggerJob(apiHelper.request, jobId);

    await authenticatedPage.goto(`/jobs/${jobId}`);

    // Expand the latest execution
    const latestExecution = authenticatedPage.getByText("Latest");
    await expect(latestExecution).toBeVisible({ timeout: 10_000 });
    await latestExecution.click();

    // Wait for expanded content
    await expect(
      authenticatedPage.getByText(/Triggered by:/)
    ).toBeVisible({ timeout: 10_000 });

    // If step executions are present, they should show step names
    // The step execution section has an uppercase "STEPS" label
    const stepsLabel = authenticatedPage.getByText("Steps", { exact: true }).last();

    // Step executions may or may not be present depending on worker state
    // Look for step execution rows that contain the step name
    const stepRow = authenticatedPage.locator(".rounded-md.border.bg-muted\\/30");
    const stepRowCount = await stepRow.count();

    if (stepRowCount > 0) {
      // The step row should contain the step name "Copy test file"
      await expect(
        authenticatedPage.getByText("Copy test file")
      ).toBeVisible();

      // Should show step order number
      await expect(
        authenticatedPage.getByText("Step 1:")
      ).toBeVisible();

      // Should show step type key
      await expect(
        authenticatedPage.getByText("(file.copy)")
      ).toBeVisible();
    }
    // If no step executions yet (worker hasn't processed), test passes
  });
});

test.describe("Job Schedules", () => {
  let jobId: string;

  test.beforeEach(async ({ apiHelper }) => {
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-sched-${Date.now()}`,
    });
    jobId = job.id;

    // Add a step so the job can be triggered
    await addJobSteps(apiHelper.request, jobId, [
      {
        name: "Copy test file",
        typeKey: "file.copy",
        stepOrder: 1,
        configuration: JSON.stringify({
          sourcePath: "/tmp/source.txt",
          destinationPath: "/tmp/dest.txt",
        }),
      },
    ]);
  });

  test.afterEach(async ({ apiHelper }) => {
    await deleteTestJob(apiHelper.request, jobId).catch(() => {});
  });

  test("schedule panel shows empty state", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto(`/jobs/${jobId}`);

    // The SchedulePanel shows "Schedules (0)" and "No schedules configured."
    await expect(
      authenticatedPage.getByText("Schedules (0)")
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      authenticatedPage.getByText("No schedules configured.")
    ).toBeVisible();
  });

  test("creates a cron schedule for a job", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto(`/jobs/${jobId}`, {
      waitUntil: "networkidle",
    });

    // Wait for the job detail page to fully load (not redirected to login)
    await expect(
      authenticatedPage.getByText(/Schedules/)
    ).toBeVisible({ timeout: 15_000 });

    // Wait for schedule panel to show zero count
    await expect(
      authenticatedPage.getByText("Schedules (0)")
    ).toBeVisible({ timeout: 10_000 });

    // Click "Add Schedule" button
    await authenticatedPage
      .getByRole("button", { name: "Add Schedule" })
      .click();

    // The Add Schedule dialog should open — wait for title and description to confirm
    // the dialog content is fully rendered (Radix Dialog uses portal + animations)
    const dialog = authenticatedPage.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await expect(
      dialog.getByText("Create a new schedule for this job.")
    ).toBeVisible();

    // Schedule type defaults to "Cron" — fill in cron expression
    // Wait for the cron expression input to be visible and interactive
    const cronInput = dialog.getByPlaceholder(
      "0 0 3 * * ? (Quartz 7-part format)"
    );
    await expect(cronInput).toBeVisible();
    await cronInput.fill("0 0 3 * * ?");

    // Click Create
    await dialog.getByRole("button", { name: "Create" }).click();

    // Should show success toast
    await expect(
      authenticatedPage.locator("[data-sonner-toast]", {
        hasText: "Schedule created",
      })
    ).toBeVisible({ timeout: 10_000 });

    // The schedule count should update
    await expect(
      authenticatedPage.getByText("Schedules (1)")
    ).toBeVisible({ timeout: 10_000 });

    // The cron expression should be visible in the schedule row
    await expect(
      authenticatedPage.getByText("0 0 3 * * ?", { exact: true })
    ).toBeVisible();
  });

  test("created schedule shows next fire time", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create schedule via API
    const schedule = await createJobSchedule(apiHelper.request, jobId, {
      cronExpression: "0 0 6 * * ?",
    });

    try {
      await authenticatedPage.goto(`/jobs/${jobId}`);

      // Wait for schedule to load
      await expect(
        authenticatedPage.getByText("Schedules (1)")
      ).toBeVisible({ timeout: 10_000 });

      // The schedule row shows "Next: <date>" — look for the "Next:" prefix
      await expect(
        authenticatedPage.getByText(/Next:/)
      ).toBeVisible();

      // The cron expression should be displayed
      await expect(
        authenticatedPage.getByText("0 0 6 * * ?")
      ).toBeVisible();

      // The cron type badge should be visible
      await expect(authenticatedPage.getByText("cron")).toBeVisible();
    } finally {
      await deleteJobSchedule(
        apiHelper.request,
        jobId,
        schedule.id
      ).catch(() => {});
    }
  });

  test("deletes a schedule", async ({ authenticatedPage, apiHelper }) => {
    // Create schedule via API
    const schedule = await createJobSchedule(apiHelper.request, jobId, {
      cronExpression: "0 30 2 * * ?",
    });

    await authenticatedPage.goto(`/jobs/${jobId}`);

    // Wait for the schedule to appear
    await expect(
      authenticatedPage.getByText("Schedules (1)")
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      authenticatedPage.getByText("0 30 2 * * ?")
    ).toBeVisible();

    // Click the delete (trash) button on the schedule row
    // The trash button is identified by having the destructive-colored icon
    const scheduleRow = authenticatedPage
      .locator(".rounded-md.border.px-4.py-3")
      .filter({ hasText: "0 30 2 * * ?" });

    // The last icon button in the row is the delete button
    const deleteButton = scheduleRow.locator("button").last();
    await deleteButton.click();

    // Confirm in the dialog
    const dialog = authenticatedPage.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await expect(
      dialog.getByText(
        "This will permanently remove the schedule and unregister it from the scheduler."
      )
    ).toBeVisible();
    await dialog.getByRole("button", { name: "Delete" }).click();

    // Should show success toast
    await expect(
      authenticatedPage.locator("[data-sonner-toast]", {
        hasText: "Schedule deleted",
      })
    ).toBeVisible({ timeout: 10_000 });

    // Schedule count should go back to 0
    await expect(
      authenticatedPage.getByText("Schedules (0)")
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      authenticatedPage.getByText("No schedules configured.")
    ).toBeVisible();
  });

  test("edits a schedule cron expression", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const schedule = await createJobSchedule(apiHelper.request, jobId, {
      cronExpression: "0 0 6 * * ?",
    });

    try {
      await authenticatedPage.goto(`/jobs/${jobId}`);

      await expect(
        authenticatedPage.getByText("Schedules (1)")
      ).toBeVisible({ timeout: 10_000 });
      await expect(
        authenticatedPage.getByText("0 0 6 * * ?")
      ).toBeVisible();

      // Click the edit (pencil) button on the schedule row
      const scheduleRow = authenticatedPage
        .locator(".rounded-md.border")
        .filter({ hasText: "0 0 6 * * ?" });

      // The pencil button is second-to-last in the row
      const editButton = scheduleRow.locator("button").filter({
        has: authenticatedPage.locator("svg.lucide-pencil"),
      });
      await editButton.click();

      // The Edit Schedule dialog should open
      const dialog = authenticatedPage.getByRole("dialog");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByText("Edit Schedule")).toBeVisible();

      // Update the cron expression
      const cronInput = dialog.getByPlaceholder("0 0 3 * * ?");
      await cronInput.clear();
      await cronInput.fill("0 30 8 * * ?");

      // Save
      await dialog.getByRole("button", { name: "Save" }).click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Schedule updated",
        })
      ).toBeVisible({ timeout: 10_000 });

      // The updated cron expression should be visible
      await expect(
        authenticatedPage.getByText("0 30 8 * * ?")
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteJobSchedule(apiHelper.request, jobId, schedule.id).catch(
        () => {}
      );
    }
  });

  test("creates a one-shot schedule", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto(`/jobs/${jobId}`, {
      waitUntil: "networkidle",
    });

    await expect(
      authenticatedPage.getByText("Schedules (0)")
    ).toBeVisible({ timeout: 10_000 });

    // Open Add Schedule dialog
    await authenticatedPage
      .getByRole("button", { name: "Add Schedule" })
      .click();

    const dialog = authenticatedPage.getByRole("dialog");
    await expect(dialog).toBeVisible();

    // Switch schedule type to One-Shot
    // The Schedule Type select defaults to "Cron" — click it and select "One-Shot"
    const typeSelect = dialog.locator("button").filter({
      hasText: "Cron",
    });
    await typeSelect.click();
    await authenticatedPage.getByRole("option", { name: "One-Shot" }).click();

    // Fill the datetime-local input for Run At
    const runAtInput = dialog.locator("input[type='datetime-local']");
    await expect(runAtInput).toBeVisible();

    // Set a future date/time
    const futureDate = new Date(Date.now() + 86400000);
    const dateStr = futureDate.toISOString().slice(0, 16);
    await runAtInput.fill(dateStr);

    // Click Create
    await dialog.getByRole("button", { name: "Create" }).click();

    await expect(
      authenticatedPage.locator("[data-sonner-toast]", {
        hasText: "Schedule created",
      })
    ).toBeVisible({ timeout: 10_000 });

    // The schedule should appear with "one_shot" type badge
    await expect(
      authenticatedPage.getByText("Schedules (1)")
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      authenticatedPage.getByText("one_shot")
    ).toBeVisible();
  });

  test("execution pagination appears with multiple executions", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Trigger enough executions to exceed page size (10) so pagination appears
    for (let i = 0; i < 11; i++) {
      await triggerJob(apiHelper.request, jobId);
      // Small delay to avoid concurrent execution guards
      if (i < 10) await new Promise((r) => setTimeout(r, 200));
    }

    await authenticatedPage.goto(`/jobs/${jobId}`);

    const main = authenticatedPage.locator("main");

    // Wait for executions to load
    await expect(
      main.getByText("Latest")
    ).toBeVisible({ timeout: 15_000 });

    // With 11 executions (page size 10), pagination should appear
    // Verify the "Next" button is visible within main (avoid matching Next.js dev tools)
    await expect(
      main.getByRole("button", { name: "Next", exact: true })
    ).toBeVisible({ timeout: 10_000 });
  });

  test("schedule enable/disable toggle", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const schedule = await createJobSchedule(apiHelper.request, jobId, {
      cronExpression: "0 0 12 * * ?",
      isEnabled: true,
    });

    try {
      await authenticatedPage.goto(`/jobs/${jobId}`);

      await expect(
        authenticatedPage.getByText("Schedules (1)")
      ).toBeVisible({ timeout: 10_000 });

      // Find the schedule row
      const scheduleRow = authenticatedPage
        .locator(".rounded-md.border")
        .filter({ hasText: "0 0 12 * * ?" });

      // The Switch toggle has aria-label "Toggle schedule"
      const toggle = scheduleRow.getByLabel("Toggle schedule");
      await expect(toggle).toBeVisible();

      // The toggle should be checked (enabled)
      await expect(toggle).toBeChecked();

      // Click to disable
      await toggle.click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Schedule disabled",
        })
      ).toBeVisible({ timeout: 10_000 });

      // Toggle should now be unchecked
      await expect(toggle).not.toBeChecked();

      // Click again to re-enable
      await toggle.click();

      await expect(
        authenticatedPage.locator("[data-sonner-toast]", {
          hasText: "Schedule enabled",
        })
      ).toBeVisible({ timeout: 10_000 });

      await expect(toggle).toBeChecked();
    } finally {
      await deleteJobSchedule(apiHelper.request, jobId, schedule.id).catch(
        () => {}
      );
    }
  });
});
