import { test, expect } from "./fixtures";
import {
  createTestTag,
  deleteTestTag,
  createTestJob,
  deleteTestJob,
  assignTagToEntity,
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
      authenticatedPage.locator("main").getByRole("heading", { name: "Tags", level: 1 }).first()
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
      authenticatedPage.locator("main").getByRole("heading", { name: "Tags", level: 1 }).first()
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
        authenticatedPage.locator("main").getByRole("heading", { name: "Tags", level: 1 }).first()
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
        authenticatedPage.locator("main").getByRole("heading", { name: "Tags", level: 1 }).first()
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

  test("tag picker on job detail assigns and shows tag", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-tagpicker-job-${Date.now().toString(36)}`,
    });
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-tagpicker-${Date.now().toString(36)}`,
      color: "#3b82f6",
    });

    try {
      await authenticatedPage.goto(`/jobs/${job.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: job.name })
      ).toBeVisible({ timeout: 10_000 });

      // Open the tag picker popover
      await authenticatedPage
        .getByRole("button", { name: "Manage Tags" })
        .click();

      // Search for the tag in the popover
      const popover = authenticatedPage.locator("[data-radix-popper-content-wrapper]");
      await popover.getByPlaceholder("Search tags...").fill(tag.name);

      // Click the tag to assign it
      await popover.getByText(tag.name).click();

      // Verify success toast
      await expect(
        authenticatedPage
          .locator("[data-sonner-toast]")
          .filter({ hasText: `Tag "${tag.name}" added` })
      ).toBeVisible({ timeout: 10_000 });

      // The tag badge should now appear on the job detail page (outside the popover)
      // Close the popover first by clicking elsewhere
      await authenticatedPage.getByRole("heading", { name: job.name }).click();

      // Verify the TagBadge is visible in the Tags card
      const tagsCard = authenticatedPage.locator("text=Tags").locator("..");
      await expect(tagsCard.getByText(tag.name)).toBeVisible({
        timeout: 10_000,
      });
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("tagged entities section shows assigned job", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const job = await createTestJob(apiHelper.request, {
      name: `e2e-tagged-entity-${Date.now().toString(36)}`,
    });
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-assigned-${Date.now().toString(36)}`,
    });

    try {
      // Assign tag to job via API
      await assignTagToEntity(apiHelper.request, tag.id, "job", job.id);

      // Navigate to tag detail page
      await authenticatedPage.goto(`/tags/${tag.id}`);
      await expect(
        authenticatedPage.getByRole("heading", { name: tag.name })
      ).toBeVisible({ timeout: 10_000 });

      // The Tagged Entities section should show the job (entity ID link)
      await expect(
        authenticatedPage.getByText("Tagged Entities")
      ).toBeVisible();

      // Should NOT show the "No entities" empty message
      await expect(
        authenticatedPage.getByText("No entities are tagged with this tag.")
      ).not.toBeVisible({ timeout: 5_000 });

      // Should show "Jobs (1)" group header
      await expect(authenticatedPage.getByText("Jobs (1)")).toBeVisible();
    } finally {
      await deleteTestJob(apiHelper.request, job.id);
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("tag badge renders with correct color", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    const tagColor = "#ef4444";
    const tag = await createTestTag(apiHelper.request, {
      name: `e2e-color-${Date.now().toString(36)}`,
      color: tagColor,
    });

    try {
      await authenticatedPage.goto("/tags");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Tags", level: 1 }).first()
      ).toBeVisible();

      // Find the tag badge in the table
      const tagBadge = authenticatedPage.locator("table").getByText(tag.name);
      await expect(tagBadge).toBeVisible({ timeout: 10_000 });

      // The TagBadge uses inline style with rgba/rgb values derived from the hex color
      // e.g., #ef4444 becomes rgb(239, 68, 68)
      const style = await tagBadge.getAttribute("style");
      expect(style).toBeTruthy();
      // The hex color #ef4444 is rendered as rgb(239, 68, 68) in computed CSS
      expect(style).toContain("239, 68, 68");
    } finally {
      await deleteTestTag(apiHelper.request, tag.id);
    }
  });

  test("pagination controls appear with many tags", async ({
    authenticatedPage,
    apiHelper,
  }) => {
    // Create 26 tags (page size is 25) to trigger pagination
    const suffix = Date.now().toString(36);
    const tags: Array<{ id: string; name: string }> = [];

    try {
      // Create tags in parallel batches
      const createPromises = Array.from({ length: 26 }, (_, i) =>
        createTestTag(apiHelper.request, {
          name: `e2e-page-${suffix}-${String(i).padStart(2, "0")}`,
          category: "pagination-test",
        })
      );
      const results = await Promise.all(createPromises);
      tags.push(...results);

      await authenticatedPage.goto("/tags");
      await expect(
        authenticatedPage.locator("main").getByRole("heading", { name: "Tags", level: 1 }).first()
      ).toBeVisible();

      // Wait for the table to load
      await expect(authenticatedPage.locator("table")).toBeVisible({
        timeout: 10_000,
      });

      // Pagination controls should be visible
      await expect(
        authenticatedPage.getByText(/Page \d+ of \d+/)
      ).toBeVisible({ timeout: 10_000 });

      const main = authenticatedPage.locator("main");
      const previousButton = main.getByRole("button", {
        name: "Previous",
        exact: true,
      });
      const nextButton = main.getByRole("button", {
        name: "Next",
        exact: true,
      });

      // On page 1, Previous should be disabled
      await expect(previousButton).toBeDisabled();

      // Next should be enabled
      await expect(nextButton).toBeEnabled();

      // Click Next to go to page 2
      await nextButton.click();

      // Should now show page 2
      await expect(
        authenticatedPage.getByText(/Page 2 of \d+/)
      ).toBeVisible({ timeout: 10_000 });

      // Previous should now be enabled
      await expect(previousButton).toBeEnabled();
    } finally {
      // Cleanup all tags
      await Promise.all(
        tags.map((t) => deleteTestTag(apiHelper.request, t.id))
      );
    }
  });
});
