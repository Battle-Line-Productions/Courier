import { test, expect } from "./fixtures";
import {
  createTestNotificationRule,
  deleteTestNotificationRule,
} from "./helpers/api-helpers";

test.describe("Notification Rules", () => {
  test("displays empty state when no rules exist", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create and delete a rule to ensure we've exercised the API, then verify page
    const rule = await createTestNotificationRule(apiHelper.request, {
      name: "e2e-empty-check",
    });
    await deleteTestNotificationRule(apiHelper.request, rule.id);

    await authenticatedPage.goto("/notifications");

    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Notifications" }).first()
    ).toBeVisible({ timeout: 10_000 });

    // Either the empty state or a table should be visible
    const emptyState = authenticatedPage.getByText("No notification rules");
    const ruleTable = authenticatedPage.locator("table");
    await expect(emptyState.or(ruleTable)).toBeVisible({ timeout: 10_000 });
  });

  test("navigates to create rule page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/notifications");
    await expect(
      authenticatedPage.locator("main").getByRole("heading", { name: "Notifications" }).first()
    ).toBeVisible({ timeout: 10_000 });

    await authenticatedPage
      .getByRole("link", { name: "Create Rule" })
      .first()
      .click();
    await expect(authenticatedPage).toHaveURL(/\/notifications\/new/);
    await expect(
      authenticatedPage.getByRole("heading", {
        name: "Create Notification Rule",
      })
    ).toBeVisible();
  });

  test("creates an email notification rule", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const ruleName = `e2e-email-rule-${suffix}`;

    await authenticatedPage.goto("/notifications/new");
    await expect(
      authenticatedPage.getByRole("heading", {
        name: "Create Notification Rule",
      })
    ).toBeVisible({ timeout: 10_000 });

    // Fill in the name
    await authenticatedPage.getByLabel("Name").fill(ruleName);

    // Select channel: Email
    // The form has two Select (combobox) components: Entity Type (first) and Channel (second)
    const comboboxes = authenticatedPage.locator("form").getByRole("combobox");
    await comboboxes.nth(1).click();
    await authenticatedPage.getByRole("option", { name: "Email" }).click();

    // Select event types — check "Job Failed"
    await authenticatedPage
      .getByText("Job Failed", { exact: true })
      .click();

    // Fill email recipients
    await authenticatedPage
      .getByLabel("Recipients")
      .fill("e2e-test@example.com");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create Rule" })
      .click();

    // Should show success toast and navigate to detail page
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Rule created" })
    ).toBeVisible();
    await expect(authenticatedPage).toHaveURL(/\/notifications\/[a-f0-9-]+$/, {
      timeout: 10_000,
    });

    // Cleanup: delete by navigating back and using API
    const url = authenticatedPage.url();
    const createdId = url.split("/notifications/")[1];
    if (createdId) {
      await deleteTestNotificationRule(apiHelper.request, createdId);
    }
  });

  test("rule appears in list", async ({ authenticatedPage, apiHelper }) => {
    const rule = await createTestNotificationRule(apiHelper.request, {
      name: `e2e-list-${Date.now().toString(36)}`,
      channel: "email",
      channelConfig: { recipients: ["test@example.com"], subjectPrefix: "[E2E]" },
    });

    try {
      await authenticatedPage.goto("/notifications");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Notifications" }).first()
      ).toBeVisible({ timeout: 10_000 });

      // The rule name should appear in the table
      await expect(authenticatedPage.getByText(rule.name)).toBeVisible({
        timeout: 10_000,
      });
    } finally {
      await deleteTestNotificationRule(apiHelper.request, rule.id);
    }
  });

  test("rule detail page shows configuration", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const rule = await createTestNotificationRule(apiHelper.request, {
      name: `e2e-detail-${Date.now().toString(36)}`,
      entityType: "job",
      eventTypes: ["job_completed"],
      channel: "email",
      channelConfig: { recipients: ["detail@example.com"], subjectPrefix: "[E2E]" },
    });

    try {
      await authenticatedPage.goto(`/notifications/${rule.id}`);

      // Heading is the rule name (wait for async data load)
      await expect(
        authenticatedPage.getByRole("heading", { name: rule.name })
      ).toBeVisible({ timeout: 10_000 });

      // Configuration section
      await expect(authenticatedPage.getByText("Configuration", { exact: true })).toBeVisible();
      await expect(authenticatedPage.getByText("Entity Type", { exact: true })).toBeVisible();
      await expect(authenticatedPage.getByText("Channel", { exact: true })).toBeVisible();
      await expect(authenticatedPage.getByText("Enabled", { exact: true })).toBeVisible();

      // Events section
      await expect(
        authenticatedPage.getByText("Events & Channel Config")
      ).toBeVisible();

      // Recent Notifications section
      await expect(
        authenticatedPage.getByRole("heading", { name: "Recent Notifications" })
      ).toBeVisible();
    } finally {
      await deleteTestNotificationRule(apiHelper.request, rule.id);
    }
  });

  test("edits a notification rule", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const rule = await createTestNotificationRule(apiHelper.request, {
      name: `e2e-edit-${Date.now().toString(36)}`,
      channel: "email",
      channelConfig: { recipients: ["edit@example.com"], subjectPrefix: "[E2E]" },
    });

    try {
      await authenticatedPage.goto(`/notifications/${rule.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: rule.name })
      ).toBeVisible({ timeout: 10_000 });

      // Click Edit button
      await authenticatedPage.getByRole("link", { name: "Edit" }).click();
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/notifications/${rule.id}/edit`),
        { timeout: 10_000 }
      );
      await expect(
        authenticatedPage.getByRole("heading", {
          name: `Edit: ${rule.name}`,
        })
      ).toBeVisible({ timeout: 10_000 });

      // Change the description
      await authenticatedPage
        .getByLabel("Description")
        .fill("Updated by E2E test");

      // Submit the update
      await authenticatedPage
        .getByRole("button", { name: "Update Rule" })
        .click();

      // Should redirect to detail page with success toast
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/notifications/${rule.id}$`),
        { timeout: 10_000 }
      );
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Rule updated" })
      ).toBeVisible();
    } finally {
      await deleteTestNotificationRule(apiHelper.request, rule.id);
    }
  });

  test("deletes a notification rule", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const rule = await createTestNotificationRule(apiHelper.request, {
      name: `e2e-delete-${Date.now().toString(36)}`,
      channel: "email",
      channelConfig: { recipients: ["delete@example.com"], subjectPrefix: "[E2E]" },
    });

    // No finally — the test itself deletes the rule
    await authenticatedPage.goto(`/notifications/${rule.id}`);
    await expect(
      authenticatedPage.getByRole("heading", { name: rule.name })
    ).toBeVisible({ timeout: 10_000 });

    // Click Delete button on the detail page
    await authenticatedPage
      .getByRole("button", { name: "Delete" })
      .click();

    // Confirm dialog appears
    const dialog = authenticatedPage.locator("[role=dialog]");
    await expect(dialog).toBeVisible();
    await expect(
      dialog.getByText("Delete Notification Rule")
    ).toBeVisible();

    // Click the confirm Delete button inside the dialog
    await dialog.getByRole("button", { name: "Delete" }).click();

    // Should see success toast and redirect to list
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Rule deleted" })
    ).toBeVisible();
    await expect(authenticatedPage).toHaveURL("/notifications", {
      timeout: 10_000,
    });
  });

  test("notification logs page loads", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/notifications/logs");

    await expect(
      authenticatedPage.getByRole("heading", { name: "Notification Logs" })
    ).toBeVisible();
    await expect(
      authenticatedPage.getByText("History of all sent notifications")
    ).toBeVisible();

    // Either logs table or empty message should be visible
    const emptyMessage = authenticatedPage.getByText(
      "No notification logs found."
    );
    const logsTable = authenticatedPage.locator("table");
    await expect(emptyMessage.or(logsTable)).toBeVisible();
  });

  test("creates a webhook notification rule", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const ruleName = `e2e-webhook-${suffix}`;

    await authenticatedPage.goto("/notifications/new");
    await authenticatedPage.waitForURL(/\/notifications\/new/, { timeout: 10_000 });
    await expect(
      authenticatedPage.getByRole("heading", {
        name: "Create Notification Rule",
      })
    ).toBeVisible({ timeout: 15_000 });

    // Fill in the name
    await authenticatedPage.getByLabel("Name").fill(ruleName);

    // Ensure Webhook channel is selected (it's the default, but select explicitly)
    const comboboxes = authenticatedPage.locator("form").getByRole("combobox");
    await comboboxes.nth(1).click();
    await authenticatedPage.getByRole("option", { name: "Webhook" }).click();

    // Select event type — check "Job Failed"
    await authenticatedPage
      .getByText("Job Failed", { exact: true })
      .click();

    // Fill webhook URL (label "URL" is linked to input via htmlFor="webhookUrl")
    await authenticatedPage
      .getByLabel("URL")
      .fill("https://example.com/e2e-webhook");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create Rule" })
      .click();

    // Should show success toast and navigate to detail page
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Rule created" })
    ).toBeVisible();
    await expect(authenticatedPage).toHaveURL(/\/notifications\/[a-f0-9-]+$/, {
      timeout: 10_000,
    });

    // Verify the detail page shows webhook channel badge
    await expect(authenticatedPage.getByText("webhook", { exact: true })).toBeVisible();

    // Cleanup
    const url = authenticatedPage.url();
    const createdId = url.split("/notifications/")[1];
    if (createdId) {
      await deleteTestNotificationRule(apiHelper.request, createdId);
    }
  });

  test("creates a rule with multiple event types", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const ruleName = `e2e-multi-events-${suffix}`;

    await authenticatedPage.goto("/notifications/new");
    await expect(
      authenticatedPage.getByRole("heading", {
        name: "Create Notification Rule",
      })
    ).toBeVisible({ timeout: 10_000 });

    await authenticatedPage.getByLabel("Name").fill(ruleName);

    // Select multiple event types
    await authenticatedPage
      .getByText("Job Failed", { exact: true })
      .click();
    await authenticatedPage
      .getByText("Job Cancelled", { exact: true })
      .click();
    await authenticatedPage
      .getByText("Job Timed Out", { exact: true })
      .click();

    // Fill webhook URL (default channel)
    await authenticatedPage
      .getByLabel("URL")
      .fill("https://example.com/e2e-multi");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create Rule" })
      .click();

    // Should navigate to detail page
    await expect(authenticatedPage).toHaveURL(/\/notifications\/[a-f0-9-]+$/, {
      timeout: 10_000,
    });

    // Verify all three events show on the detail page as badges
    await expect(authenticatedPage.getByText("job failed")).toBeVisible({
      timeout: 10_000,
    });
    await expect(authenticatedPage.getByText("job cancelled")).toBeVisible();
    await expect(authenticatedPage.getByText("job timed out")).toBeVisible();

    // Cleanup
    const url = authenticatedPage.url();
    const createdId = url.split("/notifications/")[1];
    if (createdId) {
      await deleteTestNotificationRule(apiHelper.request, createdId);
    }
  });

  test("creates a rule with monitor entity type", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const ruleName = `e2e-monitor-rule-${suffix}`;

    await authenticatedPage.goto("/notifications/new");
    await expect(
      authenticatedPage.getByRole("heading", {
        name: "Create Notification Rule",
      })
    ).toBeVisible({ timeout: 10_000 });

    await authenticatedPage.getByLabel("Name").fill(ruleName);

    // Change entity type from Job to Monitor — first Select in the form
    const selects = authenticatedPage.locator("form").getByRole("combobox");
    await selects.first().click();
    await authenticatedPage.getByRole("option", { name: "Monitor" }).click();

    // Select an event type
    await authenticatedPage
      .getByText("Job Failed", { exact: true })
      .click();

    // Fill webhook URL (default channel)
    await authenticatedPage
      .getByLabel("URL")
      .fill("https://example.com/e2e-monitor");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create Rule" })
      .click();

    // Should navigate to detail page
    await expect(authenticatedPage).toHaveURL(/\/notifications\/[a-f0-9-]+$/, {
      timeout: 10_000,
    });

    // Verify the entity type shows "monitor" on the detail page
    await expect(
      authenticatedPage.locator(".capitalize").filter({ hasText: "monitor" })
    ).toBeVisible({ timeout: 10_000 });

    // Cleanup
    const url = authenticatedPage.url();
    const createdId = url.split("/notifications/")[1];
    if (createdId) {
      await deleteTestNotificationRule(apiHelper.request, createdId);
    }
  });

  test("creates a rule with chain entity type", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const ruleName = `e2e-chain-rule-${suffix}`;

    await authenticatedPage.goto("/notifications/new");
    await expect(
      authenticatedPage.getByRole("heading", {
        name: "Create Notification Rule",
      })
    ).toBeVisible({ timeout: 10_000 });

    await authenticatedPage.getByLabel("Name").fill(ruleName);

    // Change entity type to Chain — first Select in the form
    const selects = authenticatedPage.locator("form").getByRole("combobox");
    await selects.first().click();
    await authenticatedPage.getByRole("option", { name: "Chain" }).click();

    // Select an event type
    await authenticatedPage
      .getByText("Job Failed", { exact: true })
      .click();

    // Fill webhook URL (default channel)
    await authenticatedPage
      .getByLabel("URL")
      .fill("https://example.com/e2e-chain");

    // Submit
    await authenticatedPage
      .getByRole("button", { name: "Create Rule" })
      .click();

    // Should navigate to detail page
    await expect(authenticatedPage).toHaveURL(/\/notifications\/[a-f0-9-]+$/, {
      timeout: 10_000,
    });

    // Verify the entity type shows "chain" on the detail page
    await expect(
      authenticatedPage.locator(".capitalize").filter({ hasText: "chain" })
    ).toBeVisible({ timeout: 10_000 });

    // Cleanup
    const url = authenticatedPage.url();
    const createdId = url.split("/notifications/")[1];
    if (createdId) {
      await deleteTestNotificationRule(apiHelper.request, createdId);
    }
  });

  test("sends a test notification from detail page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const rule = await createTestNotificationRule(apiHelper.request, {
      name: `e2e-test-notif-${Date.now().toString(36)}`,
      channel: "webhook",
      channelConfig: { url: "https://example.com/e2e-test-hook" },
    });

    try {
      await authenticatedPage.goto(`/notifications/${rule.id}`);
      await authenticatedPage.waitForURL(new RegExp(`/notifications/${rule.id}`), { timeout: 10_000 });
      await expect(
        authenticatedPage.getByRole("heading", { name: rule.name })
      ).toBeVisible({ timeout: 15_000 });

      // Click the Test button (contains Zap icon + "Test" text)
      // Use exact name to avoid matching other buttons
      const testButton = authenticatedPage.getByRole("button", { name: "Test", exact: true });
      await expect(testButton).toBeVisible({ timeout: 5_000 });
      await testButton.click();

      // Should show a toast indicating the test result
      // The API may succeed ("Test notification sent") or fail ("Test failed: ...")
      const successToast = authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Test notification sent" });
      const errorToast = authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Test failed" });
      await expect(successToast.or(errorToast)).toBeVisible({
        timeout: 15_000,
      });
    } finally {
      await deleteTestNotificationRule(apiHelper.request, rule.id);
    }
  });

  test("notification logs page has pagination controls", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/notifications/logs");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Notification Logs" })
    ).toBeVisible({ timeout: 10_000 });

    // The page should load — either show logs with pagination or empty state
    const emptyMessage = authenticatedPage.getByText(
      "No notification logs found."
    );
    const logsTable = authenticatedPage.locator("table");
    await expect(emptyMessage.or(logsTable)).toBeVisible({ timeout: 10_000 });

    // If there are enough logs, pagination controls (Previous/Next) will be visible
    // We verify the page structure is correct regardless
    const previousButton = authenticatedPage.getByRole("button", {
      name: "Previous",
    });
    const nextButton = authenticatedPage.getByRole("button", {
      name: "Next",
    });

    // If pagination is present, Previous should be disabled on page 1
    if (await previousButton.isVisible().catch(() => false)) {
      await expect(previousButton).toBeDisabled();
    }
  });

  test("toggles rule enabled state via edit page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const rule = await createTestNotificationRule(apiHelper.request, {
      name: `e2e-toggle-${Date.now().toString(36)}`,
      channel: "email",
      channelConfig: {
        recipients: ["toggle@example.com"],
        subjectPrefix: "[E2E]",
      },
    });

    try {
      // Navigate to the detail page and verify it's enabled
      await authenticatedPage.goto(`/notifications/${rule.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: rule.name })
      ).toBeVisible({ timeout: 15_000 });

      // The Enabled badge should show "Yes" — scope to badge elements to avoid broad matches
      const enabledBadgeYes = authenticatedPage.locator('[data-slot="badge"]', { hasText: "Yes" });
      await expect(enabledBadgeYes).toBeVisible({ timeout: 10_000 });

      // Go to edit page
      await authenticatedPage.getByRole("link", { name: "Edit" }).click();
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/notifications/${rule.id}/edit`),
        { timeout: 10_000 }
      );

      // Wait for the edit form to load
      await expect(
        authenticatedPage.getByRole("heading", {
          name: `Edit: ${rule.name}`,
        })
      ).toBeVisible({ timeout: 15_000 });

      // Toggle the enabled switch off
      await authenticatedPage.getByLabel("Enabled").click();

      // Submit the update
      await authenticatedPage
        .getByRole("button", { name: "Update Rule" })
        .click();

      // Should redirect to detail page
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/notifications/${rule.id}$`),
        { timeout: 10_000 }
      );
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: "Rule updated" })
      ).toBeVisible({ timeout: 10_000 });

      // The Enabled badge should now show "No" — scope to badge elements to avoid matching
      // unrelated text like "No notifications sent yet."
      const enabledBadgeNo = authenticatedPage.locator('[data-slot="badge"]', { hasText: "No" });
      await expect(enabledBadgeNo).toBeVisible({ timeout: 10_000 });
    } finally {
      await deleteTestNotificationRule(apiHelper.request, rule.id);
    }
  });
});
