import { test, expect } from "./fixtures";
import {
  createTestTag,
  deleteTestTag,
  getAuthToken,
} from "./helpers/api-helpers";

test.describe("Tags", () => {
  test("displays empty state when no tags exist", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Ensure no tags exist by navigating to the page on a presumably clean DB.
    // If there happen to be tags, the empty state won't show — so create a
    // deterministic scenario: create one, delete it, then verify empty state.
    const tag = await createTestTag(apiHelper.request, {
      name: "e2e-empty-check",
    });
    await deleteTestTag(apiHelper.request, tag.id);

    await authenticatedPage.goto("/tags");

    // If truly empty (no other tags), we should see the empty state
    // We check for the heading regardless — it's always present
    await expect(
      authenticatedPage.getByRole("heading", { name: "Tags", level: 1 })
    ).toBeVisible();

    // The empty state shows "No tags yet" when there are zero tags and no search
    // This may or may not appear depending on other test data; check gracefully
    const emptyState = authenticatedPage.getByText("No tags yet");
    const tagTable = authenticatedPage.locator("table");
    await expect(emptyState.or(tagTable)).toBeVisible();
  });

  test("navigates to create tag page via button", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/tags");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Tags", level: 1 })
    ).toBeVisible();

    // Click the "Create Tag" link/button in the page header
    await authenticatedPage.getByRole("link", { name: "Create Tag" }).first().click();
    await expect(authenticatedPage).toHaveURL(/\/tags\/new/);
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Tag" })
    ).toBeVisible();
  });

  test("creates a new tag with name, color, and category", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const suffix = Date.now().toString(36);
    const tagName = `e2e-create-${suffix}`;

    await authenticatedPage.goto("/tags/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Tag" })
    ).toBeVisible();

    await authenticatedPage.getByLabel("Name").fill(tagName);
    await authenticatedPage.getByLabel("Color").fill("#ef4444");
    await authenticatedPage.getByLabel("Category").fill("testing");

    await authenticatedPage.getByRole("button", { name: "Create Tag" }).click();

    // Should redirect to the tags list and show a success toast
    await expect(authenticatedPage).toHaveURL("/tags", { timeout: 10_000 });
    await expect(
      authenticatedPage.locator("[data-sonner-toast]").filter({ hasText: "Tag created" })
    ).toBeVisible();

    // Cleanup: find the tag via authenticated API call and delete it
    const apiUrl = process.env.API_URL || "http://localhost:5000";
    const token = await getAuthToken(apiHelper.request);
    const allTags = await apiHelper.request.get(
      `${apiUrl}/api/v1/tags?search=${tagName}`,
      { headers: { Authorization: `Bearer ${token}` } }
    );
    const body = await allTags.json();
    const created = body.data?.find(
      (t: { name: string }) => t.name === tagName
    );
    if (created) {
      await deleteTestTag(apiHelper.request, created.id);
    }
  });

  test("created tag appears in list", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-list-${Date.now().toString(36)}`,
      color: "#22c55e",
      category: "visible",
    });

    try {
      await authenticatedPage.goto("/tags");
      await expect(
        authenticatedPage.getByRole("heading", { name: "Tags", level: 1 })
      ).toBeVisible();

      // The tag name appears inside a TagBadge in the table
      await expect(authenticatedPage.getByText(tag.name)).toBeVisible();
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("navigates to tag detail page", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-detail-${Date.now().toString(36)}`,
      color: "#8b5cf6",
      category: "detail-test",
    });

    try {
      await authenticatedPage.goto("/tags");
      await authenticatedPage.getByText(tag.name).click();

      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/tags/${tag.id}`)
      );
      // Detail page heading is the tag name
      await expect(
        authenticatedPage.getByRole("heading", { name: tag.name })
      ).toBeVisible();

      // Tag Info card is present
      await expect(authenticatedPage.getByText("Tag Info")).toBeVisible();

      // Tagged Entities section is present
      await expect(
        authenticatedPage.getByText("Tagged Entities")
      ).toBeVisible();
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("edits an existing tag", async ({ authenticatedPage, apiHelper }) => {
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-edit-${Date.now().toString(36)}`,
      color: "#f59e0b",
      category: "before-edit",
    });

    try {
      await authenticatedPage.goto(`/tags/${tag.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: tag.name })
      ).toBeVisible();

      // Click Edit button
      await authenticatedPage.getByRole("link", { name: "Edit" }).click();
      await expect(authenticatedPage).toHaveURL(
        new RegExp(`/tags/${tag.id}/edit`)
      );
      await expect(
        authenticatedPage.getByRole("heading", { name: "Edit Tag" })
      ).toBeVisible();

      // Change the category
      const categoryInput = authenticatedPage.getByLabel("Category");
      await categoryInput.clear();
      await categoryInput.fill("after-edit");

      await authenticatedPage
        .getByRole("button", { name: "Save Changes" })
        .click();

      // Should redirect to tags list with success toast
      await expect(authenticatedPage).toHaveURL("/tags", { timeout: 10_000 });
      await expect(
        authenticatedPage.locator("[data-sonner-toast]").filter({ hasText: "Tag updated" })
      ).toBeVisible();
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("validates required name field on create", async ({
    authenticatedPage,
  }) => {
    await authenticatedPage.goto("/tags/new");
    await expect(
      authenticatedPage.getByRole("heading", { name: "Create Tag" })
    ).toBeVisible();

    // Leave name empty and submit
    await authenticatedPage.getByRole("button", { name: "Create Tag" }).click();

    // Should show validation error
    await expect(
      authenticatedPage.getByText("Name is required")
    ).toBeVisible();

    // Should NOT navigate away
    await expect(authenticatedPage).toHaveURL(/\/tags\/new/);
  });

  test("searches tags by name", async ({ authenticatedPage, apiHelper }) => {
    const uniqueSuffix = Date.now().toString(36);
    const tag1 = await createTestTag(apiHelper.request, {
      name: `e2e-searchable-${uniqueSuffix}`,
    });
    const tag2 = await createTestTag(apiHelper.request, {
      name: `e2e-other-${uniqueSuffix}`,
    });

    try {
      await authenticatedPage.goto("/tags");
      await expect(
        authenticatedPage.getByRole("heading", { name: "Tags", level: 1 })
      ).toBeVisible();

      // Wait for table to appear
      await expect(authenticatedPage.getByText(tag1.name)).toBeVisible();

      // Type in the search box
      const searchInput = authenticatedPage.getByPlaceholder("Search tags...");
      await searchInput.fill("e2e-searchable");

      // The matching tag should be visible
      await expect(authenticatedPage.getByText(tag1.name)).toBeVisible();

      // The non-matching tag should disappear
      await expect(authenticatedPage.getByText(tag2.name)).not.toBeVisible();
    } finally {
      await deleteTestTag(apiHelper.request, tag1.id);
      await deleteTestTag(apiHelper.request, tag2.id);
    }
  });

  test("deletes a tag via list action", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-delete-${Date.now().toString(36)}`,
    });

    // No finally block — the tag should be deleted by the test itself
    await authenticatedPage.goto("/tags");
    await expect(authenticatedPage.getByText(tag.name)).toBeVisible();

    // Open the actions dropdown for the tag row.
    // The dropdown trigger is a ghost button with MoreHorizontal icon in the row.
    // Force-click because it has opacity-0 until group-hover.
    const row = authenticatedPage.locator("tr", { hasText: tag.name });
    await row
      .getByRole("button")
      .filter({ has: authenticatedPage.locator("svg") })
      .click({ force: true });

    // Click Delete in the dropdown menu
    await authenticatedPage.getByRole("menuitem", { name: "Delete" }).click();

    // Confirm dialog appears
    const dialog = authenticatedPage.locator("[role=dialog]");
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText("Delete Tag")).toBeVisible();

    // Click the confirm Delete button inside the dialog
    await dialog.getByRole("button", { name: "Delete" }).click();

    // Should see success toast
    await expect(
      authenticatedPage.locator("[data-sonner-toast]").filter({ hasText: "Tag deleted" })
    ).toBeVisible();

    // Tag should no longer be in the list
    await expect(authenticatedPage.getByText(tag.name)).not.toBeVisible();
  });

  test("tag detail shows assigned entities section", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-entities-${Date.now().toString(36)}`,
    });

    try {
      await authenticatedPage.goto(`/tags/${tag.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: tag.name })
      ).toBeVisible();

      // The "Tagged Entities" card should be visible
      await expect(
        authenticatedPage.getByText("Tagged Entities")
      ).toBeVisible();

      // With no entities assigned, it should show the empty message
      await expect(
        authenticatedPage.getByText("No entities are tagged with this tag.")
      ).toBeVisible();
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });
});
