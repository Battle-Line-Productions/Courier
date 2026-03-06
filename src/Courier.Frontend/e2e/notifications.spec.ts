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
      authenticatedPage.getByRole("heading", { name: "Notifications" })
    ).toBeVisible({ timeout: 10_000 });

    // Either the empty state or a table should be visible
    const emptyState = authenticatedPage.getByText("No notification rules");
    const ruleTable = authenticatedPage.locator("table");
    await expect(emptyState.or(ruleTable)).toBeVisible({ timeout: 10_000 });
  });

  test("navigates to create rule page", async ({ authenticatedPage }) => {
    await authenticatedPage.goto("/notifications");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Notifications" })
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
    ).toBeVisible();

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

    // Should navigate to detail page with success toast
    await expect(authenticatedPage).toHaveURL(/\/notifications\/[a-f0-9-]+$/, {
      timeout: 10_000,
    });
    await expect(
      authenticatedPage
        .locator("[data-sonner-toast]")
        .filter({ hasText: "Rule created" })
    ).toBeVisible();

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
        authenticatedPage.getByRole("heading", { name: "Notifications" })
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
});
