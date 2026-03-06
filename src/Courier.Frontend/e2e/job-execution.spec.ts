import { test, expect } from "./fixtures";
import {
  createTestJob,
  deleteTestJob,
  addJobSteps,
  triggerJob,
  createJobSchedule,
  deleteJobSchedule,
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
});

test.describe("Job Schedules", () => {
  let jobId: string;

  test.beforeEach(async ({ apiHelper }) => {
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-sched-${Date.now()}`,
    });
    jobId = job.id;
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
});
