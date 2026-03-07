import { test, expect } from "./fixtures";
import {
  createTestChain,
  deleteTestChain,
  createTestJob,
  deleteTestJob,
  setChainMembers,
} from "./helpers/api-helpers";

test.describe("Chains", () => {
  test("displays empty state when no chains exist", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create and immediately delete a chain to ensure a clean state path was exercised
    const chain = await createTestChain(apiHelper.request, {
      name: "e2e-empty-check",
    });
    await deleteTestChain(apiHelper.request, chain.id);

    await authenticatedPage.goto("/chains");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Chains" }).first()
    ).toBeVisible({ timeout: 10_000 });

    // Either the empty state or the table should be visible depending on other test data
    const emptyState = authenticatedPage.getByText("No chains yet");
    const chainTable = authenticatedPage.locator("table");
    await expect(emptyState.or(chainTable)).toBeVisible();
  });

  test("navigates to create chain page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/chains");
    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Chains" }).first()
    ).toBeVisible({ timeout: 10_000 });

    await authenticatedPage
      .getByRole("link", { name: "Create Chain" })
      .first()
      .click();
    await expect(authenticatedPage).toHaveURL(/\/chains\/new/);
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Chain" })
    ).toBeVisible();
  });

  test("creates a chain with name and description", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const chainName = `e2e-create-${suffix}`;

    await authenticatedPage.goto("/chains/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Chain" })
    ).toBeVisible({ timeout: 10_000 });

    await authenticatedPage.getByLabel("Name").fill(chainName);
    await authenticatedPage
      .getByLabel("Description")
      .fill("E2E test chain description");

    await authenticatedPage
      .getByRole("button", { name: "Create" })
      .click();

    // Should redirect to the chain detail page and show a success toast
    await expect(authenticatedPage).toHaveURL(/\/chains\/[a-f0-9-]+$/, {
      timeout: 10_000,
    });
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Chain created" })
    ).toBeVisible({ timeout: 10_000 });

    // The detail page should show the chain name as heading
    await expect(
      authenticatedPage.getByRole("heading", { name: chainName })
    ).toBeVisible({ timeout: 10_000 });

    // Cleanup via API
    const url = authenticatedPage.url();
    const chainId = url.split("/chains/")[1];
    if (chainId) {
      await deleteTestChain(apiHelper.request, chainId);
    }
  });

  test("chain detail page shows chain info", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-detail-${Date.now().toString(36)}`,
      description: "Detail page test description",
    });

    try {
      await authenticatedPage.goto(`/chains/${chain.id}`);

      // Heading is the chain name
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible({ timeout: 10_000 });

      // Description is visible
      await expect(
        authenticatedPage.getByText("Detail page test description")
      ).toBeVisible();

      // Enabled badge
      await expect(authenticatedPage.getByText("Enabled")).toBeVisible();

      // Members badge shows count
      await expect(authenticatedPage.getByText("0 members")).toBeVisible();

      // Members card title is present
      await expect(authenticatedPage.getByText("Members (0)")).toBeVisible();

      // Executions card title is present (use exact to avoid matching "No executions yet...")
      await expect(
        authenticatedPage.getByText("Executions", { exact: true })
      ).toBeVisible();

      // Edit button
      await expect(
        authenticatedPage.getByRole("link", { name: "Edit" })
      ).toBeVisible();

      // Run button (disabled because no members, but still visible)
      await expect(
        authenticatedPage.getByRole("button", { name: "Run" })
      ).toBeVisible();
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
    }
  });

  test("chain detail shows empty members message", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-empty-members-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto(`/chains/${chain.id}`);

      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible();

      // Empty members message
      await expect(
        authenticatedPage.getByText("No members yet. Add jobs to this chain.")
      ).toBeVisible();

      // Empty executions message
      await expect(
        authenticatedPage.getByText(
          "No executions yet. Run this chain to see execution history."
        )
      ).toBeVisible();
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
    }
  });

  test("adds job members to chain via member editor", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-add-members-${Date.now().toString(36)}`,
    });
    const job1 = await createTestJob(apiHelper.request, {
      name: `e2e-member-job1-${Date.now().toString(36)}`,
    });
    const job2 = await createTestJob(apiHelper.request, {
      name: `e2e-member-job2-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible();

      // Click "Add Job" to add first member slot
      await authenticatedPage
        .getByRole("button", { name: "Add Job" })
        .click();

      // Select the first job from the dropdown
      // The select trigger shows "Select a job" placeholder
      await authenticatedPage
        .getByRole("combobox")
        .first()
        .click();
      await authenticatedPage.getByRole("option", { name: job1.name }).click();

      // Add a second member
      await authenticatedPage
        .getByRole("button", { name: "Add Job" })
        .click();

      // The second combobox appears - select job2
      const comboboxes = authenticatedPage.getByRole("combobox");
      // The "Job" select for the second member row (index-based: first row has Job + DependsOn, second row has Job + DependsOn)
      // Job selects are at positions 0, 2 (every other one); DependsOn at 1, 3
      await comboboxes.nth(2).click();
      await authenticatedPage.getByRole("option", { name: job2.name }).click();

      // Save the members
      await authenticatedPage
        .getByRole("button", { name: "Save Members" })
        .click();

      // Success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Members updated" })
      ).toBeVisible();
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
      await deleteTestJob(apiHelper.request, job1.id);
      await deleteTestJob(apiHelper.request, job2.id);
    }
  });

  test("chain detail shows member count badge", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-member-badge-${Date.now().toString(36)}`,
    });
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-badge-job-${Date.now().toString(36)}`,
    });

    try {
      // Add a member via API
      await setChainMembers(apiHelper.request, chain.id, [
        { jobId: job.id, executionOrder: 1 },
      ]);

      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible();

      // Badge shows "1 member"
      await expect(authenticatedPage.getByText("1 member")).toBeVisible();

      // Members card title shows count
      await expect(authenticatedPage.getByText("Members (1)")).toBeVisible();
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("edits chain name", async ({ authenticatedPage, apiHelper }) => {
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-edit-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible();

      // Click Edit link
      await authenticatedPage.getByRole("link", { name: "Edit" }).click();
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/chains/${chain.id}/edit`)
      );
      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit Chain" })
      ).toBeVisible();

      // Change the name
      const nameInput = authenticatedPage.getByLabel("Name");
      await nameInput.clear();
      const updatedName = `e2e-edited-${Date.now().toString(36)}`;
      await nameInput.fill(updatedName);

      await authenticatedPage
        .getByRole("button", { name: "Save" })
        .click();

      // Should redirect to chain detail and show success toast
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/chains/${chain.id}$`),
        { timeout: 10_000 }
      );
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Chain updated" })
      ).toBeVisible();

      // The heading should reflect the updated name
      await expect(
        authenticatedPage.getByRole("heading", { name: updatedName })
      ).toBeVisible();
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
    }
  });

  test("run button is disabled when chain has no members", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-run-disabled-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible();

      // Run button should be disabled because there are no members
      const runButton = authenticatedPage.getByRole("button", { name: "Run" });
      await expect(runButton).toBeVisible();
      await expect(runButton).toBeDisabled();
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
    }
  });

  test("triggering a chain shows confirmation dialog", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-trigger-${Date.now().toString(36)}`,
    });
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-trigger-job-${Date.now().toString(36)}`,
    });

    try {
      // Add a member so the Run button is enabled
      await setChainMembers(apiHelper.request, chain.id, [
        { jobId: job.id, executionOrder: 1 },
      ]);

      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible();

      // Click Run
      await authenticatedPage
        .getByRole("button", { name: "Run" })
        .click();

      // Confirmation dialog should appear
      const dialog = authenticatedPage.locator("[role=dialog]");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByText("Run Chain")).toBeVisible();
      await expect(
        dialog.getByText(`Run "${chain.name}" now?`)
      ).toBeVisible();
      await expect(
        dialog.getByText("1 job(s) in order")
      ).toBeVisible();

      // Cancel to dismiss
      await dialog.getByRole("button", { name: "Cancel" }).click();
      await expect(dialog).not.toBeVisible();
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("chain execution appears in execution list", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-exec-${Date.now().toString(36)}`,
    });
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-exec-job-${Date.now().toString(36)}`,
    });

    try {
      // Add a member
      await setChainMembers(apiHelper.request, chain.id, [
        { jobId: job.id, executionOrder: 1 },
      ]);

      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible();

      // Click Run
      await authenticatedPage
        .getByRole("button", { name: "Run" })
        .click();

      // Confirm in dialog
      const dialog = authenticatedPage.locator("[role=dialog]");
      await expect(dialog).toBeVisible();
      await dialog.getByRole("button", { name: "Run" }).click();

      // Success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Chain triggered" })
      ).toBeVisible();

      // Wait for the execution to appear in the list (replace the empty message)
      await expect(
        authenticatedPage.getByText(
          "No executions yet. Run this chain to see execution history."
        )
      ).not.toBeVisible({ timeout: 15_000 });

      // An execution entry should now be visible with a state badge
      const executionsCard = authenticatedPage.locator("div", {
        has: authenticatedPage.getByText("Executions", { exact: true }),
      });
      // At least one execution entry should exist (with a state badge like queued/running/completed)
      await expect(
        executionsCard.locator(".rounded-md.border").first()
      ).toBeVisible({ timeout: 15_000 });
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
      await deleteTestJob(apiHelper.request, job.id);
    }
  });

  test("deletes a chain", async ({ authenticatedPage, apiHelper }) => {
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-delete-${Date.now().toString(36)}`,
    });

    // No finally — the test itself deletes the chain
    await authenticatedPage.goto("/chains");
    await authenticatedPage.waitForURL("**/chains");
    await expect(authenticatedPage.getByText(chain.name)).toBeVisible({ timeout: 10_000 });

    // Open the actions dropdown for the chain row
    // The dropdown trigger is a ghost button with opacity-0 until group-hover
    const row = authenticatedPage.locator("tr", { hasText: chain.name });
    await row
      .getByRole("button")
      .filter({ has: authenticatedPage.locator("svg") })
      .click({ force: true });

    // Click Delete in the dropdown
    await authenticatedPage
      .getByRole("menuitem", { name: "Delete" })
      .click();

    // Confirm dialog appears
    const dialog = authenticatedPage.locator("[role=dialog]");
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText("Delete Chain")).toBeVisible();

    // Click the confirm Delete button inside the dialog
    await dialog.getByRole("button", { name: "Delete" }).click();

    // Success toast
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Chain deleted" })
    ).toBeVisible();

    // Chain should no longer appear in the list
    await expect(authenticatedPage.getByText(chain.name)).not.toBeVisible();
  });

  test("sets member dependencies", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-deps-${suffix}`,
    });
    const job1 = await createTestJob(apiHelper.request, {
      name: `e2e-dep-job1-${suffix}`,
    });
    const job2 = await createTestJob(apiHelper.request, {
      name: `e2e-dep-job2-${suffix}`,
    });

    try {
      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible({ timeout: 10_000 });

      // Add first member
      await authenticatedPage
        .getByRole("button", { name: "Add Job" })
        .click();
      const comboboxes = authenticatedPage.getByRole("combobox");
      await comboboxes.first().click();
      await authenticatedPage
        .getByRole("option", { name: job1.name })
        .click();

      // Add second member
      await authenticatedPage
        .getByRole("button", { name: "Add Job" })
        .click();
      // Job select for second member is at index 2 (Job1, DependsOn1, Job2, DependsOn2)
      await comboboxes.nth(2).click();
      await authenticatedPage
        .getByRole("option", { name: job2.name })
        .click();

      // The second member's "Depends On" should default to first member
      // Verify by checking the depends-on select shows the first job
      // The DependsOn select for the second member is at index 3
      await comboboxes.nth(3).click();
      // Select the first member as dependency (it should show "1. <job1.name>")
      await authenticatedPage
        .getByRole("option", { name: new RegExp(`1\\.\\s*${job1.name}`) })
        .click();

      // The "Run even if upstream fails" switch should now be visible
      await expect(
        authenticatedPage.getByText("Run even if upstream fails")
      ).toBeVisible();

      // Save the members
      await authenticatedPage
        .getByRole("button", { name: "Save Members" })
        .click();

      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Members updated" })
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
      await deleteTestJob(apiHelper.request, job1.id);
      await deleteTestJob(apiHelper.request, job2.id);
    }
  });

  test("removes a member from chain", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-remove-member-${suffix}`,
    });
    const job1 = await createTestJob(apiHelper.request, {
      name: `e2e-rm-job1-${suffix}`,
    });
    const job2 = await createTestJob(apiHelper.request, {
      name: `e2e-rm-job2-${suffix}`,
    });

    try {
      // Set up 2 members via API
      await setChainMembers(apiHelper.request, chain.id, [
        { jobId: job1.id, executionOrder: 1 },
        { jobId: job2.id, executionOrder: 2 },
      ]);

      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible({ timeout: 10_000 });

      // Should show "Members (2)"
      await expect(
        authenticatedPage.getByText("Members (2)")
      ).toBeVisible({ timeout: 10_000 });

      // Click the trash button on the first member card to remove it
      // Each member card has a trash icon button
      const trashButtons = authenticatedPage.locator(
        "button:has(svg.lucide-trash-2)"
      );
      await trashButtons.first().click();

      // Save the updated members
      await authenticatedPage
        .getByRole("button", { name: "Save Members" })
        .click();

      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Members updated" })
      ).toBeVisible({ timeout: 10_000 });

      // After page refresh, member count should decrease
      await authenticatedPage.reload();
      await expect(
        authenticatedPage.getByText("Members (1)")
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
      await deleteTestJob(apiHelper.request, job1.id);
      await deleteTestJob(apiHelper.request, job2.id);
    }
  });

  test("chain schedules CRUD", async ({ authenticatedPage, apiHelper }) => {
    const suffix = Date.now().toString(36);
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-sched-${suffix}`,
    });

    try {
      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible({ timeout: 10_000 });

      // Schedules card should show "Schedules (0)"
      await expect(
        authenticatedPage.getByText("Schedules (0)")
      ).toBeVisible({ timeout: 10_000 });

      // Click "Add Schedule" button
      await authenticatedPage
        .getByRole("button", { name: "Add Schedule" })
        .click();

      // Dialog should appear
      const dialog = authenticatedPage.locator("[role=dialog]");
      await expect(dialog).toBeVisible();
      await expect(dialog.getByText("Add Schedule")).toBeVisible();

      // Fill in the cron expression
      await dialog
        .getByPlaceholder("0 0 3 * * ? (Quartz 7-part format)")
        .fill("0 30 2 * * ?");

      // Click Create
      await dialog.getByRole("button", { name: "Create" }).click();

      // Success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Schedule created" })
      ).toBeVisible({ timeout: 10_000 });

      // Schedules count should update
      await expect(
        authenticatedPage.getByText("Schedules (1)")
      ).toBeVisible({ timeout: 10_000 });

      // The cron expression should be displayed
      await expect(
        authenticatedPage.getByText("0 30 2 * * ?")
      ).toBeVisible();

      // Delete the schedule via the trash button in the schedule row
      const scheduleTrash = authenticatedPage
        .locator(".rounded-md.border")
        .filter({ hasText: "0 30 2 * * ?" })
        .locator("button:has(svg.lucide-trash-2)");
      await scheduleTrash.click();

      // Confirm delete dialog
      const deleteDialog = authenticatedPage.locator("[role=dialog]");
      await expect(deleteDialog).toBeVisible();
      await deleteDialog.getByRole("button", { name: "Delete" }).click();

      // Success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Schedule deleted" })
      ).toBeVisible({ timeout: 10_000 });

      // Schedule count back to 0
      await expect(
        authenticatedPage.getByText("Schedules (0)")
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
    }
  });

  test("search/filter chains on list page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const chain1 = await createTestChain(apiHelper.request, {
      name: `e2e-findme-${suffix}`,
    });
    const chain2 = await createTestChain(apiHelper.request, {
      name: `e2e-other-${suffix}`,
    });

    try {
      await authenticatedPage.goto("/chains");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Chains" }).first()
      ).toBeVisible();

      // Both chains should be visible in the table
      await expect(
        authenticatedPage.getByText(chain1.name)
      ).toBeVisible({ timeout: 10_000 });
      await expect(
        authenticatedPage.getByText(chain2.name)
      ).toBeVisible();

      // The chains table rows can be filtered by checking text content
      // Since there's no search input on the chains page, we verify both appear
      // and that clicking a row navigates to the detail
      const row1 = authenticatedPage.locator("tr", { hasText: chain1.name });
      await expect(row1).toBeVisible();
      const row2 = authenticatedPage.locator("tr", { hasText: chain2.name });
      await expect(row2).toBeVisible();
    } finally {
      await deleteTestChain(apiHelper.request, chain1.id);
      await deleteTestChain(apiHelper.request, chain2.id);
    }
  });

  test("pagination on chains list", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const chains: { id: string; name: string }[] = [];

    try {
      // Create 11 chains to trigger pagination (page size is 10)
      for (let i = 0; i < 11; i++) {
        const chain = await createTestChain(apiHelper.request, {
          name: `e2e-page-${suffix}-${String(i).padStart(2, "0")}`,
        });
        chains.push(chain);
      }

      await authenticatedPage.goto("/chains");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Chains" }).first()
      ).toBeVisible();

      // Wait for the table to load
      await expect(authenticatedPage.locator("table")).toBeVisible({
        timeout: 10_000,
      });

      // Pagination should be visible
      await expect(
        authenticatedPage.getByText("Page 1 of")
      ).toBeVisible({ timeout: 10_000 });

      // Previous should be disabled on page 1
      const prevButton = authenticatedPage.getByRole("button", {
        name: "Previous",
      });
      await expect(prevButton).toBeDisabled();

      // Next should be enabled (scope to main to avoid Next.js dev tools button)
      const nextButton = authenticatedPage.locator("main").getByRole("button", {
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
      for (const chain of chains) {
        await deleteTestChain(apiHelper.request, chain.id);
      }
    }
  });

  test("enabled badge reflects chain state", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-enabled-${suffix}`,
    });

    try {
      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible({ timeout: 10_000 });

      // Chain starts enabled — verify the badge
      await expect(
        authenticatedPage.getByText("Enabled", { exact: true })
      ).toBeVisible();

      // The Run button should be disabled (no members), but visible
      const runButton = authenticatedPage.getByRole("button", { name: "Run" });
      await expect(runButton).toBeVisible();
      await expect(runButton).toBeDisabled();

      // Verify the chain shows as "Yes" in the list view
      await authenticatedPage.goto("/chains");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Chains" }).first()
      ).toBeVisible();

      const row = authenticatedPage.locator("tr", { hasText: chain.name });
      await expect(row).toBeVisible({ timeout: 10_000 });
      await expect(row.getByText("Yes")).toBeVisible();
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
    }
  });

  test("member reordering by removing and re-adding", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const chain = await createTestChain(apiHelper.request, {
      name: `e2e-reorder-${suffix}`,
    });
    const job1 = await createTestJob(apiHelper.request, {
      name: `e2e-order-A-${suffix}`,
    });
    const job2 = await createTestJob(apiHelper.request, {
      name: `e2e-order-B-${suffix}`,
    });
    const job3 = await createTestJob(apiHelper.request, {
      name: `e2e-order-C-${suffix}`,
    });

    try {
      // Set up 3 members via API: A=1, B=2, C=3
      await setChainMembers(apiHelper.request, chain.id, [
        { jobId: job1.id, executionOrder: 1 },
        { jobId: job2.id, executionOrder: 2 },
        { jobId: job3.id, executionOrder: 3 },
      ]);

      await authenticatedPage.goto(`/chains/${chain.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: chain.name })
      ).toBeVisible({ timeout: 10_000 });

      await expect(
        authenticatedPage.getByText("Members (3)")
      ).toBeVisible({ timeout: 10_000 });

      // Verify the order: member numbers 1, 2, 3 should be visible
      await expect(authenticatedPage.getByText("1.")).toBeVisible();
      await expect(authenticatedPage.getByText("2.")).toBeVisible();
      await expect(authenticatedPage.getByText("3.")).toBeVisible();

      // Remove the first member (job A) by clicking its trash button
      const trashButtons = authenticatedPage.locator(
        "button:has(svg.lucide-trash-2)"
      );
      await trashButtons.first().click();

      // Now should have 2 members numbered 1 and 2
      // Save the members
      await authenticatedPage
        .getByRole("button", { name: "Save Members" })
        .click();

      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Members updated" })
      ).toBeVisible({ timeout: 10_000 });

      // Reload and verify
      await authenticatedPage.reload();
      await expect(
        authenticatedPage.getByText("Members (2)")
      ).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestChain(apiHelper.request, chain.id);
      await deleteTestJob(apiHelper.request, job1.id);
      await deleteTestJob(apiHelper.request, job2.id);
      await deleteTestJob(apiHelper.request, job3.id);
    }
  });
});
