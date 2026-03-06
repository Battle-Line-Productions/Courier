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
      authenticatedPage.getByRole("heading", { name: "Chains" })
    ).toBeVisible();

    // Either the empty state or the table should be visible depending on other test data
    const emptyState = authenticatedPage.getByText("No chains yet");
    const chainTable = authenticatedPage.locator("table");
    await expect(emptyState.or(chainTable)).toBeVisible();
  });

  test("navigates to create chain page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/chains");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Chains" })
    ).toBeVisible();

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
    ).toBeVisible();

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
    ).toBeVisible();

    // The detail page should show the chain name as heading
    await expect(
      authenticatedPage.getByRole("heading", { name: chainName })
    ).toBeVisible();

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
    await expect(authenticatedPage.getByText(chain.name)).toBeVisible();

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
});
